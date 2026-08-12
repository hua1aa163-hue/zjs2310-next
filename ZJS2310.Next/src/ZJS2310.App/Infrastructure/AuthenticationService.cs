using System.Security.Cryptography;
using System.Text;

namespace ZJS2310.App.Infrastructure;

public sealed record UserSummary(string UserName, string Role);

public sealed class AuthenticationService
{
    private readonly JsonFileStore<List<UserAccount>> _store;
    private List<UserAccount> _users;

    public AuthenticationService(string path)
    {
        _store = new JsonFileStore<List<UserAccount>>(path);
        _users = _store.Load() ?? [CreateAccount("admin", "1234", "管理员")];
        _store.Save(_users);
    }

    public IReadOnlyList<UserSummary> GetUsers() =>
        _users.OrderBy(user => user.UserName, StringComparer.OrdinalIgnoreCase)
            .Select(user => new UserSummary(user.UserName, user.Role)).ToArray();

    public bool Authenticate(string userName, string password, out UserSummary? user)
    {
        user = null;
        var account = _users.FirstOrDefault(item => string.Equals(item.UserName, userName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (account is null)
        {
            return false;
        }

        var actual = Hash(password, Convert.FromBase64String(account.Salt));
        if (!CryptographicOperations.FixedTimeEquals(actual, Convert.FromBase64String(account.PasswordHash)))
        {
            return false;
        }

        user = new UserSummary(account.UserName, account.Role);
        return true;
    }

    public void Upsert(string userName, string password, string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        if (password.Length < 4)
        {
            throw new InvalidOperationException("密码至少需要 4 个字符。");
        }

        var normalized = userName.Trim();
        _users.RemoveAll(item => string.Equals(item.UserName, normalized, StringComparison.OrdinalIgnoreCase));
        _users.Add(CreateAccount(normalized, password, string.IsNullOrWhiteSpace(role) ? "操作员" : role.Trim()));
        _store.Save(_users);
    }

    public void Delete(string userName)
    {
        var account = _users.FirstOrDefault(item => string.Equals(item.UserName, userName, StringComparison.OrdinalIgnoreCase));
        if (account is null)
        {
            return;
        }

        if (string.Equals(account.UserName, "admin", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("内置管理员 admin 不能删除。");
        }

        _users.Remove(account);
        _store.Save(_users);
    }

    private static UserAccount CreateAccount(string userName, string password, string role)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        return new UserAccount
        {
            UserName = userName,
            Role = role,
            Salt = Convert.ToBase64String(salt),
            PasswordHash = Convert.ToBase64String(Hash(password, salt))
        };
    }

    private static byte[] Hash(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, 100_000, HashAlgorithmName.SHA256, 32);

    public sealed class UserAccount
    {
        public string UserName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Salt { get; set; } = string.Empty;
        public string Role { get; set; } = "操作员";
    }
}
