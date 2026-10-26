using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppBridge.Core;
using AppBridge.Core.Ipatool;
using AppBridge.Services;

namespace AppBridge.ViewModels;

public enum AccountState
{
    Checking,
    SignedOut,
    SignedIn,
    Unavailable,
}

/// <summary>
/// App Store sign-in. The password is handed to ipatool for this one request and then cleared;
/// AppBridge never writes it anywhere.
/// </summary>
public sealed partial class AccountViewModel(IAppStoreClient ipatool) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignedIn), nameof(IsSignedOut), nameof(IsChecking), nameof(TitleBarText), nameof(IsUnavailable))]
    public partial AccountState State { get; set; } = AccountState.Checking;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TitleBarText), nameof(SignedInText))]
    public partial string Email { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SignedInText))]
    public partial string Name { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    public partial string InputEmail { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    public partial string InputPassword { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    public partial string InputCode { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SignInButtonText))]
    public partial bool IsCodeRequired { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; set; }

    public bool IsSignedIn => State == AccountState.SignedIn;

    public bool IsSignedOut => State == AccountState.SignedOut;

    public bool IsChecking => State == AccountState.Checking;

    public bool IsUnavailable => State == AccountState.Unavailable;

    public bool IsIdle => !IsBusy;

    public bool HasError => !string.IsNullOrEmpty(Error);

    public string TitleBarText => State == AccountState.SignedIn ? Email : Loc.Get("Account_SignIn");

    public string SignedInText => string.IsNullOrWhiteSpace(Name) ? Email : $"{Name}  ·  {Email}";

    public string SignInButtonText => IsCodeRequired ? Loc.Get("Account_Verify") : Loc.Get("Account_SignIn");

    public async Task RefreshAsync()
    {
        if (!ipatool.IsAvailable)
        {
            State = AccountState.Unavailable;
            return;
        }

        State = AccountState.Checking;
        try
        {
            var account = await ipatool.GetAccountAsync();
            if (account is null)
            {
                State = AccountState.SignedOut;
                return;
            }

            Email = account.Email;
            Name = account.Name;
            State = AccountState.SignedIn;
        }
        catch (Exception ex)
        {
            AppLog.Warn("Could not read the App Store account.", ex);
            State = AccountState.SignedOut;
        }
    }

    private bool CanSignIn() =>
        !IsBusy && InputEmail.Trim().Length > 0 && InputPassword.Length > 0 && (!IsCodeRequired || InputCode.Trim().Length >= 6);

    [RelayCommand(CanExecute = nameof(CanSignIn))]
    private async Task SignInAsync()
    {
        IsBusy = true;
        Error = null;
        SignInCommand.NotifyCanExecuteChanged();
        try
        {
            var result = await ipatool.LoginAsync(InputEmail.Trim(), InputPassword, IsCodeRequired ? InputCode : null);
            switch (result.Status)
            {
                case LoginStatus.SignedIn:
                    Email = result.Account?.Email ?? InputEmail.Trim();
                    Name = result.Account?.Name ?? "";
                    State = AccountState.SignedIn;
                    ResetForm();
                    break;
                case LoginStatus.CodeRequired when IsCodeRequired:
                    Error = Loc.Get("Account_Error_Code");
                    InputCode = "";
                    break;
                case LoginStatus.CodeRequired:
                    IsCodeRequired = true;
                    break;
                default:
                    Error = FriendlyError(result.Error);
                    break;
            }
        }
        catch (IpatoolException ex)
        {
            Error = ex.Kind == IpatoolErrorKind.Network ? Loc.Get("Account_Error_Network") : FriendlyError(ex.Message);
        }
        finally
        {
            IsBusy = false;
            SignInCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Apple rejected the saved session; ask for the password again (email prefilled).</summary>
    public void MarkSessionExpired()
    {
        if (State != AccountState.SignedIn)
            return;
        InputEmail = Email;
        Error = Loc.Get("Account_Error_Expired");
        State = AccountState.SignedOut;
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        IsBusy = true;
        try
        {
            await ipatool.SignOutAsync();
        }
        catch (Exception ex)
        {
            AppLog.Warn("Sign-out failed.", ex);
        }
        finally
        {
            IsBusy = false;
        }

        Email = "";
        Name = "";
        State = AccountState.SignedOut;
    }

    [RelayCommand]
    private void CancelCode()
    {
        IsCodeRequired = false;
        InputCode = "";
        Error = null;
    }

    private void ResetForm()
    {
        InputPassword = "";
        InputCode = "";
        IsCodeRequired = false;
        Error = null;
    }

    private static string FriendlyError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return Loc.Get("Account_Error_Generic");
        var lower = error.ToLowerInvariant();
        if (lower.Contains("password") && (lower.Contains("incorrect") || lower.Contains("invalid")))
            return Loc.Get("Account_Error_Password");
        if (lower.Contains("disabled") || lower.Contains("locked"))
            return Loc.Get("Account_Error_Disabled");
        return Loc.Format("Account_Error_Apple", error.TrimEnd('.'));
    }
}
