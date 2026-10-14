using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace RdpConsole
{
    // Спільні константи й форматування для глобальної гарячої клавіші -- використовуються
    // і в налаштуваннях за замовчуванням (AppSettings), і в реєстрації (MainForm),
    // і в діалозі вибору комбінації (SettingsForm), щоб не дублювати значення.
    public static class HotkeyUtil
    {
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint DefaultVk = 0x52; // VK_R

        public static string Format(uint modifiers, uint vk)
        {
            if (vk == 0) return "(не задано)";
            var sb = new StringBuilder();
            if ((modifiers & MOD_CONTROL) != 0) sb.Append("Ctrl+");
            if ((modifiers & MOD_ALT) != 0) sb.Append("Alt+");
            if ((modifiers & MOD_SHIFT) != 0) sb.Append("Shift+");
            sb.Append(((Keys)vk).ToString());
            return sb.ToString();
        }
    }

    public class ThemeColors
    {
        public Color FormBack;
        public Color PanelBack;
        public Color Foreground;
        public Color ControlBack;
        public Color ControlFore;
        public Color ButtonBack;
        public Color ButtonFore;
        public Color ButtonBorder;
        public Color ButtonHover;
        public Color ButtonPressed;
        public Color AccentLink;
        public Color GridLines;
        public Color ZebraStripe;
    }

    // Проста система тем -- Світла / Темна / Синя (за прикладом трьох тем
    // ShadowSessionTool). Кожна тема -- набір кольорів, які рекурсивно
    // застосовуються до форми й усіх дочірніх контролів у ApplyRecursive.
    public static class ThemeManager
    {
        public const string LightThemeName = "Light";
        public const string DarkThemeName = "Dark";
        public const string BlueThemeName = "Blue";

        public static readonly ThemeColors Light = new ThemeColors
        {
            FormBack = Color.FromArgb(245, 245, 245),
            PanelBack = Color.White,
            Foreground = Color.FromArgb(30, 30, 30),
            ControlBack = Color.White,
            ControlFore = Color.FromArgb(30, 30, 30),
            ButtonBack = Color.White,
            ButtonFore = Color.FromArgb(30, 30, 30),
            ButtonBorder = Color.FromArgb(200, 200, 200),
            ButtonHover = Color.FromArgb(232, 232, 232),
            ButtonPressed = Color.FromArgb(215, 215, 215),
            AccentLink = SystemColors.GrayText,
            GridLines = Color.FromArgb(225, 225, 225),
            ZebraStripe = Color.FromArgb(245, 247, 250)
        };

        public static readonly ThemeColors Dark = new ThemeColors
        {
            FormBack = Color.FromArgb(32, 32, 32),
            PanelBack = Color.FromArgb(45, 45, 48),
            Foreground = Color.FromArgb(230, 230, 230),
            ControlBack = Color.FromArgb(37, 37, 38),
            ControlFore = Color.FromArgb(230, 230, 230),
            ButtonBack = Color.FromArgb(62, 62, 66),
            ButtonFore = Color.FromArgb(230, 230, 230),
            ButtonBorder = Color.FromArgb(80, 80, 84),
            ButtonHover = Color.FromArgb(80, 80, 84),
            ButtonPressed = Color.FromArgb(95, 95, 100),
            AccentLink = Color.FromArgb(90, 160, 220),
            GridLines = Color.FromArgb(60, 60, 63),
            ZebraStripe = Color.FromArgb(45, 45, 48)
        };

        public static readonly ThemeColors Blue = new ThemeColors
        {
            FormBack = Color.FromArgb(235, 242, 250),
            PanelBack = Color.FromArgb(222, 235, 250),
            Foreground = Color.FromArgb(20, 40, 65),
            ControlBack = Color.White,
            ControlFore = Color.FromArgb(20, 40, 65),
            ButtonBack = Color.FromArgb(210, 228, 247),
            ButtonFore = Color.FromArgb(20, 40, 65),
            ButtonBorder = Color.FromArgb(150, 185, 220),
            ButtonHover = Color.FromArgb(190, 215, 240),
            ButtonPressed = Color.FromArgb(170, 200, 230),
            AccentLink = Color.FromArgb(30, 90, 160),
            GridLines = Color.FromArgb(200, 220, 240),
            ZebraStripe = Color.FromArgb(212, 227, 245)
        };

        public static ThemeColors Get(string themeName)
        {
            if (themeName == DarkThemeName) return Dark;
            if (themeName == BlueThemeName) return Blue;
            return Light;
        }

        public static void Apply(Control root, string themeName)
        {
            var c = Get(themeName);
            ApplyRecursive(root, c);
        }

        static void ApplyRecursive(Control control, ThemeColors c)
        {
            if (control is Form)
            {
                control.BackColor = c.FormBack;
                control.ForeColor = c.Foreground;
            }
            else if (control is Panel || control is GroupBox)
            {
                control.BackColor = c.PanelBack;
                control.ForeColor = c.Foreground;
            }
            else if (control is Button)
            {
                var b = (Button)control;
                bool iconOnly = (b.Tag as string) == "iconOnly";
                b.FlatStyle = FlatStyle.Flat;
                b.BackColor = iconOnly ? c.PanelBack : c.ButtonBack;
                b.ForeColor = iconOnly ? c.Foreground : c.ButtonFore;
                b.FlatAppearance.BorderColor = c.ButtonBorder;
                b.FlatAppearance.BorderSize = iconOnly ? 0 : 1;
                b.FlatAppearance.MouseOverBackColor = c.ButtonHover;
                b.FlatAppearance.MouseDownBackColor = c.ButtonPressed;
            }
            else if (control is TextBox || control is ComboBox)
            {
                control.BackColor = c.ControlBack;
                control.ForeColor = c.ControlFore;
            }
            else if (control is ListView)
            {
                control.BackColor = c.ControlBack;
                control.ForeColor = c.ControlFore;
            }
            else if (control is TreeView)
            {
                control.BackColor = c.ControlBack;
                control.ForeColor = c.ControlFore;
            }
            else if (control is ListBox)
            {
                control.BackColor = c.ControlBack;
                control.ForeColor = c.ControlFore;
            }
            else if (control is StatusStrip)
            {
                control.BackColor = c.PanelBack;
                control.ForeColor = c.Foreground;
            }
            else if (control is LinkLabel)
            {
                var ll = (LinkLabel)control;
                ll.LinkColor = c.AccentLink;
                ll.ForeColor = c.Foreground;
                ll.BackColor = Color.Transparent;
            }
            else if (control is Label || control is CheckBox || control is RadioButton)
            {
                control.ForeColor = c.Foreground;
                control.BackColor = Color.Transparent;
            }
            else
            {
                control.BackColor = c.FormBack;
                control.ForeColor = c.Foreground;
            }

            foreach (Control child in control.Controls)
                ApplyRecursive(child, c);
        }
    }

    // Обгортка над DWM API для фону Mica (Windows 11 22H2+). На старіших системах
    // DwmSetWindowAttribute просто повертає код помилки (HRESULT != 0) -- ловимо це
    // й тихо не вмикаємо ефект, застосунок далі працює зі звичайним непрозорим фоном.
    internal static class DwmMica
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

        [StructLayout(LayoutKind.Sequential)]
        struct MARGINS
        {
            public int Left, Right, Top, Bottom;
        }

        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        const int DWMSBT_MAINWINDOW = 2; // Mica
        const int DWMSBT_NONE = 1;

        // Повертає true, якщо Mica увімкнено -- у цьому разі контрол(и), де має
        // "просвічувати" фон, потрібно пофарбувати в чистий чорний (Color.Black):
        // під час DwmExtendFrameIntoClientArea з від'ємними полями DWM трактує
        // такі пікселі як прозорі й показує крізь них розмитий матеріал Mica.
        public static bool TryEnable(IntPtr hwnd, bool darkTitleBar)
        {
            try
            {
                int dark = darkTitleBar ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

                int backdrop = DWMSBT_MAINWINDOW;
                int hr = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
                if (hr != 0) return false;

                var margins = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
                DwmExtendFrameIntoClientArea(hwnd, ref margins);
                return true;
            }
            catch (DllNotFoundException) { return false; }
            catch (EntryPointNotFoundException) { return false; }
        }

        // Вимикає Mica (повертає непрозорий фон) -- викликається, коли користувач
        // знімає позначку в Налаштуваннях без перезапуску застосунку.
        public static void Disable(IntPtr hwnd)
        {
            try
            {
                int backdrop = DWMSBT_NONE;
                DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
                int dark = 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
                var margins = new MARGINS { Left = 0, Right = 0, Top = 0, Bottom = 0 };
                DwmExtendFrameIntoClientArea(hwnd, ref margins);
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
    }

    // Облікові дані, збережені для конкретного .rdp-файлу. Host/User -- значення з самого
    // файлу: за ними пароль знаходить файл знову, якщо його перейменували чи перенесли в
    // іншу підпапку (див. PasswordManager.Reconcile).
    [DataContract]
    public class SavedCredential
    {
        [DataMember] public string Host { get; set; }
        [DataMember] public string User { get; set; }
        [DataMember] public string Password { get; set; }
    }

    // Пароль за замовчуванням для певного користувача; порожній User -- для будь-якого.
    [DataContract]
    public class DefaultCredential
    {
        [DataMember] public string User { get; set; }
        [DataMember] public string Password { get; set; }
    }

    // Налаштування поділено на дві частини:
    //  - локальні (лише цей ПК, без DataMember): папка з .rdp, розмір вікна, папка
    //    збереження налаштувань -- %AppData%\RdpConsole\local.json, без шифрування
    //    (паролів там немає);
    //  - спільні (DataMember): паролі, тема, гаряча клавіша тощо -- шифрований файл
    //    RdpConsole.dat у вибраній папці (наприклад, на Google Drive), тож ті самі
    //    налаштування доступні з різних ПК.
    [DataContract]
    public class AppSettings
    {
        public string RootFolder { get; set; }
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
        public string SettingsFolder { get; set; }

        [DataMember] public bool HierarchyView { get; set; }
        [DataMember] public uint HotkeyModifiers { get; set; }
        [DataMember] public uint HotkeyKey { get; set; }
        [DataMember] public string Theme { get; set; }
        [DataMember] public bool MicaEnabled { get; set; }
        // Ключ -- шлях .rdp відносно кореневої папки (на різних ПК корінь може бути різним).
        [DataMember] public Dictionary<string, SavedCredential> FilePasswords { get; set; }
        // Ключ -- шлях підпапки відносно кореневої папки; діє на всі файли всередині неї.
        [DataMember] public Dictionary<string, string> FolderPasswords { get; set; }
        [DataMember] public List<DefaultCredential> DefaultCredentials { get; set; }
        [DataMember] public bool StoreInWindowsCredentials { get; set; }
        // Відносні шляхи папок, файли яких (разом із підпапками) не беруть участі в пошуку.
        [DataMember] public List<string> SearchExcludedFolders { get; set; }
        // Відносні шляхи закріплених підключень -- показуються першими (група "Закріплені").
        [DataMember] public List<string> PinnedConnections { get; set; }

        public static AppSettings CreateDefault()
        {
            var s = new AppSettings
            {
                RootFolder = "",
                WindowWidth = 940,
                WindowHeight = 620,
                SettingsFolder = "",
                HierarchyView = true,
                HotkeyModifiers = HotkeyUtil.MOD_CONTROL | HotkeyUtil.MOD_ALT,
                HotkeyKey = HotkeyUtil.DefaultVk,
                Theme = ThemeManager.LightThemeName,
                MicaEnabled = false
            };
            s.Normalize();
            return s;
        }

        // Після десеріалізації колекції можуть бути null, а словники втрачають
        // регістронезалежне порівняння ключів (шляхи Windows) -- відновлюємо.
        public void Normalize()
        {
            FilePasswords = FilePasswords != null
                ? new Dictionary<string, SavedCredential>(FilePasswords, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, SavedCredential>(StringComparer.OrdinalIgnoreCase);
            FolderPasswords = FolderPasswords != null
                ? new Dictionary<string, string>(FolderPasswords, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (DefaultCredentials == null) DefaultCredentials = new List<DefaultCredential>();
            if (SearchExcludedFolders == null) SearchExcludedFolders = new List<string>();
            if (PinnedConnections == null) PinnedConnections = new List<string>();
            if (string.IsNullOrEmpty(Theme)) Theme = ThemeManager.LightThemeName;
        }

        public void CopySharedFrom(AppSettings o)
        {
            HierarchyView = o.HierarchyView;
            HotkeyModifiers = o.HotkeyModifiers;
            HotkeyKey = o.HotkeyKey;
            Theme = o.Theme;
            MicaEnabled = o.MicaEnabled;
            StoreInWindowsCredentials = o.StoreInWindowsCredentials;
            SearchExcludedFolders = o.SearchExcludedFolders == null ? null : new List<string>(o.SearchExcludedFolders);
            PinnedConnections = o.PinnedConnections == null ? null : new List<string>(o.PinnedConnections);
            FilePasswords = o.FilePasswords == null ? null : o.FilePasswords.ToDictionary(
                kv => kv.Key, kv => new SavedCredential { Host = kv.Value.Host, User = kv.Value.User, Password = kv.Value.Password });
            FolderPasswords = o.FolderPasswords == null ? null : new Dictionary<string, string>(o.FolderPasswords);
            DefaultCredentials = o.DefaultCredentials == null ? null : o.DefaultCredentials
                .Select(d => new DefaultCredential { User = d.User, Password = d.Password }).ToList();
            Normalize();
        }

        public AppSettings Clone()
        {
            var c = new AppSettings
            {
                RootFolder = RootFolder,
                WindowWidth = WindowWidth,
                WindowHeight = WindowHeight,
                SettingsFolder = SettingsFolder
            };
            c.CopySharedFrom(this);
            return c;
        }
    }

    [DataContract]
    class LocalSettings
    {
        [DataMember] public string RootFolder { get; set; }
        [DataMember] public int WindowWidth { get; set; }
        [DataMember] public int WindowHeight { get; set; }
        [DataMember] public string SettingsFolder { get; set; }
    }

    // Формат settings.json до v1.6 -- читається лише один раз, для перенесення.
    [DataContract]
    class LegacySettings
    {
        [DataMember] public string RootFolder { get; set; }
        [DataMember] public int WindowWidth { get; set; }
        [DataMember] public int WindowHeight { get; set; }
        [DataMember] public Dictionary<string, string> EncryptedPasswords { get; set; }
        [DataMember] public string DefaultEncryptedPassword { get; set; }
        [DataMember] public bool HierarchyView { get; set; }
        [DataMember] public uint HotkeyModifiers { get; set; }
        [DataMember] public uint HotkeyKey { get; set; }
        [DataMember] public string Theme { get; set; }
        [DataMember] public bool MicaEnabled { get; set; }
    }

    // Шифрування спільного файлу налаштувань: AES-256-CBC + HMAC-SHA256 (encrypt-then-MAC).
    // Ключ вшито в програму -- файл може лежати на Google Drive й читатися на будь-якому ПК
    // без введення майстер-пароля. Захищає від стороннього погляду на файл, але не від
    // того, хто має і файл, і вихідний код програми.
    internal static class SettingsCrypto
    {
        static readonly byte[] Secret =
        {
            0x7D, 0xDE, 0x75, 0xEB, 0xE8, 0xE6, 0x56, 0x9C, 0x1A, 0x2D, 0x2C, 0x19, 0x27, 0xE5, 0x0C, 0x29,
            0x2E, 0x85, 0x11, 0xBE, 0x6B, 0xCF, 0x6B, 0x98, 0xCC, 0x82, 0xAA, 0x5E, 0xD4, 0x01, 0x52, 0xEF
        };
        static readonly byte[] Magic = Encoding.ASCII.GetBytes("RDPC1");
        const int IvSize = 16;
        const int MacSize = 32;

        static byte[] DeriveKey(string purpose)
        {
            using (var sha = SHA256.Create())
            {
                return sha.ComputeHash(Secret.Concat(Encoding.ASCII.GetBytes(purpose)).ToArray());
            }
        }

        public static byte[] Encrypt(byte[] plain)
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = DeriveKey("enc");
                aes.GenerateIV();

                byte[] cipher;
                using (var enc = aes.CreateEncryptor()) cipher = enc.TransformFinalBlock(plain, 0, plain.Length);

                var body = Magic.Concat(aes.IV).Concat(cipher).ToArray();
                using (var hmac = new HMACSHA256(DeriveKey("mac")))
                {
                    return body.Concat(hmac.ComputeHash(body)).ToArray();
                }
            }
        }

        // null -- файл пошкоджено, змінено вручну або це не наш формат.
        public static byte[] Decrypt(byte[] data)
        {
            if (data == null || data.Length < Magic.Length + IvSize + 16 + MacSize) return null;
            for (int i = 0; i < Magic.Length; i++) if (data[i] != Magic[i]) return null;

            int bodyLen = data.Length - MacSize;
            byte[] expected;
            using (var hmac = new HMACSHA256(DeriveKey("mac"))) expected = hmac.ComputeHash(data, 0, bodyLen);
            int diff = 0;
            for (int i = 0; i < MacSize; i++) diff |= expected[i] ^ data[bodyLen + i];
            if (diff != 0) return null;

            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = DeriveKey("enc");
                aes.IV = data.Skip(Magic.Length).Take(IvSize).ToArray();
                int cipherStart = Magic.Length + IvSize;
                using (var dec = aes.CreateDecryptor())
                {
                    return dec.TransformFinalBlock(data, cipherStart, bodyLen - cipherStart);
                }
            }
        }
    }

    public static class SettingsManager
    {
        const string SharedFileName = "RdpConsole.dat";

        public static string LocalDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RdpConsole"); }
        }

        static string LocalPath { get { return Path.Combine(LocalDir, "local.json"); } }
        static string LegacyPath { get { return Path.Combine(LocalDir, "settings.json"); } }

        public static string SharedPath(AppSettings s)
        {
            var dir = string.IsNullOrWhiteSpace(s.SettingsFolder) ? LocalDir : s.SettingsFolder.Trim();
            return Path.Combine(dir, SharedFileName);
        }

        // Час зміни спільного файлу на момент останнього читання/запису -- щоб помітити,
        // що його змінили з іншого ПК (через Google Drive), і перечитати перед записом.
        static string stampPath;
        static DateTime stampTime;

        // Файл існує, але прочитати його не вдалося (пошкоджений, недосинхронізований
        // тощо) -- такий файл НЕ перезаписуємо, інакше втратимо всі збережені паролі.
        static string blockedPath;

        public static string LoadError { get; private set; }

        public static AppSettings Load()
        {
            var s = AppSettings.CreateDefault();
            var local = ReadJson<LocalSettings>(LocalPath);
            if (local == null && File.Exists(LegacyPath))
            {
                MigrateLegacy(s);
                return s;
            }
            if (local != null)
            {
                s.RootFolder = local.RootFolder ?? "";
                s.WindowWidth = local.WindowWidth;
                s.WindowHeight = local.WindowHeight;
                s.SettingsFolder = local.SettingsFolder ?? "";
            }
            ReloadShared(s, false);
            return s;
        }

        // Перечитує спільний файл у вже наявний об'єкт налаштувань (форми тримають на нього
        // посилання). onlyIfChanged -- лише якщо файл змінився з моменту останнього
        // читання/запису. true -- налаштування оновлено.
        public static bool ReloadShared(AppSettings s, bool onlyIfChanged)
        {
            var path = SharedPath(s);
            try
            {
                if (!File.Exists(path)) return false;
                var time = File.GetLastWriteTimeUtc(path);
                if (onlyIfChanged && string.Equals(path, stampPath, StringComparison.OrdinalIgnoreCase) && time == stampTime)
                    return false;

                var shared = ReadShared(path);
                if (shared == null)
                {
                    blockedPath = path;
                    LoadError = "Не вдалося прочитати файл налаштувань:\n" + path +
                                "\n\nФайл пошкоджено або він ще не синхронізувався. Зміни паролів і налаштувань " +
                                "не будуть записані в нього, поки файл не стане читабельним.";
                    return false;
                }

                s.CopySharedFrom(shared);
                stampPath = path;
                stampTime = time;
                if (string.Equals(blockedPath, path, StringComparison.OrdinalIgnoreCase)) blockedPath = null;
                LoadError = null;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static AppSettings ReadShared(string path)
        {
            try
            {
                var plain = SettingsCrypto.Decrypt(File.ReadAllBytes(path));
                if (plain == null) return null;
                using (var ms = new MemoryStream(plain))
                {
                    var s = (AppSettings)new DataContractJsonSerializer(typeof(AppSettings)).ReadObject(ms);
                    s.Normalize();
                    return s;
                }
            }
            catch
            {
                return null;
            }
        }

        public static void Save(AppSettings s)
        {
            SaveLocal(s);
            SaveShared(s);
        }

        public static void SaveLocal(AppSettings s)
        {
            try
            {
                Directory.CreateDirectory(LocalDir);
                WriteJson(LocalPath, new LocalSettings
                {
                    RootFolder = s.RootFolder,
                    WindowWidth = s.WindowWidth,
                    WindowHeight = s.WindowHeight,
                    SettingsFolder = s.SettingsFolder
                });
            }
            catch
            {
                // тихо ігноруємо помилки збереження (наприклад, немає прав на запис)
            }
        }

        // force -- свідомо перезаписати навіть нечитабельний файл (користувач підтвердив).
        public static bool SaveShared(AppSettings s, bool force = false)
        {
            var path = SharedPath(s);
            if (!force && string.Equals(path, blockedPath, StringComparison.OrdinalIgnoreCase)) return false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                byte[] plain;
                using (var ms = new MemoryStream())
                {
                    new DataContractJsonSerializer(typeof(AppSettings)).WriteObject(ms, s);
                    plain = ms.ToArray();
                }

                // Спершу пишемо поруч у тимчасовий файл, потім підміняємо -- щоб збій
                // посеред запису не залишив пошкоджений файл з усіма паролями.
                var tmp = path + ".tmp";
                File.WriteAllBytes(tmp, SettingsCrypto.Encrypt(plain));
                if (File.Exists(path))
                {
                    try { File.Replace(tmp, path, null); }
                    catch { File.Copy(tmp, path, true); File.Delete(tmp); }
                }
                else
                {
                    File.Move(tmp, path);
                }

                stampPath = path;
                stampTime = File.GetLastWriteTimeUtc(path);
                blockedPath = null;
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Перенесення зі старого settings.json: паролі там були зашифровані DPAPI і
        // прив'язані до повного шляху файлу -- розшифровуємо й переводимо на шляхи
        // відносно кореневої папки. Старий файл перейменовується на settings.json.bak.
        static void MigrateLegacy(AppSettings s)
        {
            var old = ReadJson<LegacySettings>(LegacyPath);
            if (old == null) return;

            s.RootFolder = old.RootFolder ?? "";
            s.WindowWidth = old.WindowWidth;
            s.WindowHeight = old.WindowHeight;
            s.HierarchyView = old.HierarchyView;
            s.MicaEnabled = old.MicaEnabled;
            if (!string.IsNullOrEmpty(old.Theme)) s.Theme = old.Theme;
            if (old.HotkeyKey != 0)
            {
                s.HotkeyModifiers = old.HotkeyModifiers;
                s.HotkeyKey = old.HotkeyKey;
            }

            if (old.EncryptedPasswords != null)
            {
                foreach (var kv in old.EncryptedPasswords)
                {
                    var pwd = PasswordManager.DecryptLegacy(kv.Value);
                    var rel = RdpScanner.MakeRelative(s.RootFolder, kv.Key);
                    if (string.IsNullOrEmpty(pwd) || rel == null) continue;

                    string host = "", user = "";
                    try { if (File.Exists(kv.Key)) RdpFileEditor.Read(kv.Key, out host, out user); } catch { }
                    s.FilePasswords[rel] = new SavedCredential { Host = host, User = user, Password = pwd };
                }
            }

            var defaultPwd = PasswordManager.DecryptLegacy(old.DefaultEncryptedPassword);
            if (!string.IsNullOrEmpty(defaultPwd))
            {
                s.DefaultCredentials.Add(new DefaultCredential { User = "", Password = defaultPwd });
            }

            SaveLocal(s);
            if (SaveShared(s))
            {
                try
                {
                    var bak = LegacyPath + ".bak";
                    if (File.Exists(bak)) File.Delete(bak);
                    File.Move(LegacyPath, bak);
                }
                catch { }
            }
        }

        static T ReadJson<T>(string path) where T : class
        {
            try
            {
                if (!File.Exists(path)) return null;
                using (var fs = File.OpenRead(path))
                {
                    return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(fs);
                }
            }
            catch
            {
                return null;
            }
        }

        static void WriteJson<T>(string path, T value)
        {
            using (var fs = File.Create(path))
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(fs, value);
            }
        }
    }

    // Облікові дані RDP-серверів у Диспетчері облікових даних Windows (Панель керування
    // -> Диспетчер облікових даних -> Облікові дані Windows) -- саме там mstsc зберігає
    // позначку "Запам'ятати мене" під ключем "TERMSRV/<адреса>".
    internal static class WindowsCredentialStore
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct CREDENTIAL
        {
            public uint Flags;
            public uint Type;
            public string TargetName;
            public string Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public string TargetAlias;
            public string UserName;
        }

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CredRead(string target, uint type, int reservedFlag, out IntPtr credentialPtr);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CredWrite(ref CREDENTIAL credential, uint flags);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CredDelete(string target, uint type, int flags);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern void CredFree(IntPtr cred);

        const uint CRED_TYPE_GENERIC = 1;
        const uint CRED_TYPE_DOMAIN_PASSWORD = 2;
        const uint CRED_TYPE_DOMAIN_VISIBLE_PASSWORD = 4;
        const uint CRED_PERSIST_LOCAL_MACHINE = 2;

        static readonly uint[] TypesToCheck = { CRED_TYPE_DOMAIN_PASSWORD, CRED_TYPE_GENERIC, CRED_TYPE_DOMAIN_VISIBLE_PASSWORD };

        // Індикатор пароля перевіряється для кожного рядка при кожному оновленні списку --
        // кешуємо результат; кеш скидається при перескануванні (F5) і після запису/видалення.
        static readonly Dictionary<string, bool> cache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public static void ClearCache()
        {
            cache.Clear();
        }

        public static bool HasStoredCredential(string hostname)
        {
            if (string.IsNullOrEmpty(hostname)) return false;
            bool result;
            if (cache.TryGetValue(hostname, out result)) return result;

            var target = "TERMSRV/" + hostname;
            result = false;
            foreach (var type in TypesToCheck)
            {
                IntPtr credPtr;
                if (CredRead(target, type, 0, out credPtr))
                {
                    CredFree(credPtr);
                    result = true;
                    break;
                }
            }
            cache[hostname] = result;
            return result;
        }

        // Той самий тип запису, що створює сам mstsc ("Запам'ятати мене") або
        // "cmdkey /add:TERMSRV/<хост>" -- mstsc підхоплює його без запиту пароля.
        public static void Write(string hostname, string user, string password)
        {
            var blob = Marshal.StringToCoTaskMemUni(password);
            try
            {
                var cred = new CREDENTIAL
                {
                    Type = CRED_TYPE_DOMAIN_PASSWORD,
                    TargetName = "TERMSRV/" + hostname,
                    UserName = user,
                    CredentialBlob = blob,
                    CredentialBlobSize = (uint)(password.Length * 2),
                    Persist = CRED_PERSIST_LOCAL_MACHINE
                };
                if (!CredWrite(ref cred, 0)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
            finally
            {
                Marshal.ZeroFreeCoTaskMemUnicode(blob);
                cache.Remove(hostname);
            }
        }

        public static bool Delete(string hostname)
        {
            if (string.IsNullOrEmpty(hostname)) return false;
            bool any = false;
            foreach (var type in TypesToCheck)
            {
                if (CredDelete("TERMSRV/" + hostname, type, 0)) any = true;
            }
            cache.Remove(hostname);
            return any;
        }
    }

    public enum PasswordSource { None, Windows, File, Folder, Default }

    public class PasswordInfo
    {
        public PasswordSource Source;
        public string Password;
        public string Description;

        // Значок у колонці "Пароль": W -- облікові дані Windows, ✓ -- власний пароль
        // підключення, 📁 -- пароль папки, • -- за замовчуванням, порожньо -- нічого.
        public string Marker
        {
            get
            {
                switch (Source)
                {
                    case PasswordSource.Windows: return "W";
                    case PasswordSource.File: return "✓";
                    case PasswordSource.Folder: return "📁";
                    case PasswordSource.Default: return "•";
                    default: return "";
                }
            }
        }
    }

    public static class PasswordManager
    {
        // Ентропія DPAPI зі старого формату (до v1.6) -- лише для перенесення паролів.
        static readonly byte[] LegacyEntropy = Encoding.UTF8.GetBytes("RdpConsole.v1.pwd");

        public static string DecryptLegacy(string encryptedBase64)
        {
            if (string.IsNullOrEmpty(encryptedBase64)) return null;
            try
            {
                var protectedBytes = Convert.FromBase64String(encryptedBase64);
                var bytes = ProtectedData.Unprotect(protectedBytes, LegacyEntropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return null;
            }
        }

        // Пріоритет:
        //  1) облікові дані Windows для цього сервера (TERMSRV/<хост>) -- нічого не
        //     підставляємо, mstsc сам ними скористається;
        //  2) власний пароль цього підключення;
        //  3) пароль папки -- найближчої вгору по дереву від файлу;
        //  4) пароль за замовчуванням -- для користувача з .rdp, інакше для будь-якого.
        public static PasswordInfo Resolve(AppSettings s, RdpEntry e)
        {
            if (WindowsCredentialStore.HasStoredCredential(e.HostName))
            {
                return new PasswordInfo
                {
                    Source = PasswordSource.Windows,
                    Description = "Облікові дані Windows (TERMSRV/" + e.HostName + ")"
                };
            }

            SavedCredential own;
            if (s.FilePasswords.TryGetValue(e.RelativePath, out own) && !string.IsNullOrEmpty(own.Password))
            {
                return new PasswordInfo { Source = PasswordSource.File, Password = own.Password, Description = "Власний пароль підключення" };
            }

            var folder = e.FolderKey;
            while (folder.Length > 0)
            {
                string folderPwd;
                if (s.FolderPasswords.TryGetValue(folder, out folderPwd) && !string.IsNullOrEmpty(folderPwd))
                {
                    return new PasswordInfo { Source = PasswordSource.Folder, Password = folderPwd, Description = "Пароль папки \"" + folder + "\"" };
                }
                var idx = folder.LastIndexOf('\\');
                folder = idx < 0 ? "" : folder.Substring(0, idx);
            }

            var def = FindDefault(s, e.User);
            if (def != null)
            {
                return new PasswordInfo
                {
                    Source = PasswordSource.Default,
                    Password = def.Password,
                    Description = "За замовчуванням (" + (string.IsNullOrWhiteSpace(def.User) ? "будь-який користувач" : def.User.Trim()) + ")"
                };
            }

            return new PasswordInfo { Source = PasswordSource.None, Description = "Пароль не задано" };
        }

        // Спершу точний збіг користувача; далі -- запис без домену ("admin") збігається з
        // "SERVER\admin" чи "admin@domain"; наостанок -- запис із порожнім користувачем.
        static DefaultCredential FindDefault(AppSettings s, string user)
        {
            var list = s.DefaultCredentials.Where(d => !string.IsNullOrEmpty(d.Password)).ToList();
            user = (user ?? "").Trim();
            if (user.Length > 0)
            {
                var exact = list.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d.User) &&
                    string.Equals(d.User.Trim(), user, StringComparison.OrdinalIgnoreCase));
                if (exact != null) return exact;

                var shortName = ShortUserName(user);
                var byShort = list.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d.User) &&
                    d.User.IndexOf('\\') < 0 && d.User.IndexOf('@') < 0 &&
                    string.Equals(d.User.Trim(), shortName, StringComparison.OrdinalIgnoreCase));
                if (byShort != null) return byShort;
            }
            return list.FirstOrDefault(d => string.IsNullOrWhiteSpace(d.User));
        }

        static string ShortUserName(string user)
        {
            var idx = user.LastIndexOf('\\');
            if (idx >= 0) user = user.Substring(idx + 1);
            var at = user.IndexOf('@');
            if (at > 0) user = user.Substring(0, at);
            return user;
        }

        public static void SetFilePassword(AppSettings s, RdpEntry e, string password)
        {
            s.FilePasswords[e.RelativePath] = new SavedCredential { Host = e.Host, User = e.User, Password = password };
        }

        // Звіряє збережені паролі файлів зі щойно просканованими .rdp:
        //  - оновлює запам'ятовані адресу/користувача, якщо їх змінили у файлі;
        //  - пароль файлу, якого вже немає (перейменовано/перенесено), переносить на файл
        //    з тією самою адресою й користувачем -- якщо такий файл рівно один і він ще
        //    не має власного пароля. Паролі зниклих файлів не видаляються.
        // true -- щось змінилося і налаштування треба зберегти.
        public static bool Reconcile(AppSettings s, List<RdpEntry> entries)
        {
            if (entries.Count == 0) return false;
            bool changed = false;
            var byRel = new Dictionary<string, RdpEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in entries) byRel[e.RelativePath] = e;

            foreach (var kv in s.FilePasswords)
            {
                RdpEntry e;
                if (!byRel.TryGetValue(kv.Key, out e)) continue;
                if (!string.Equals(kv.Value.Host ?? "", e.Host, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(kv.Value.User ?? "", e.User, StringComparison.OrdinalIgnoreCase))
                {
                    kv.Value.Host = e.Host;
                    kv.Value.User = e.User;
                    changed = true;
                }
            }

            var orphans = s.FilePasswords.Where(kv => !byRel.ContainsKey(kv.Key) && !string.IsNullOrEmpty(kv.Value.Host)).ToList();
            foreach (var kv in orphans)
            {
                var candidates = entries.Where(e =>
                    !s.FilePasswords.ContainsKey(e.RelativePath) &&
                    string.Equals(e.Host, kv.Value.Host, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(e.User, kv.Value.User ?? "", StringComparison.OrdinalIgnoreCase)).ToList();
                if (candidates.Count != 1) continue;

                s.FilePasswords.Remove(kv.Key);
                s.FilePasswords[candidates[0].RelativePath] = kv.Value;
                changed = true;
            }
            return changed;
        }
    }

    public class RdpEntry
    {
        public string FullPath;
        public string DisplayName;
        public string RelativeFolder;
        // Шлях файлу відносно кореневої папки ("Клієнт\сервер.rdp") -- ключ пароля файлу.
        public string RelativePath;
        // Відносний шлях папки файлу; "" -- корінь.
        public string FolderKey;
        // "full address" і "username" з самого .rdp.
        public string Host = "";
        public string User = "";

        // Адреса без порту -- саме так mstsc називає записи в Диспетчері облікових даних.
        public string HostName
        {
            get
            {
                var address = Host;
                if (string.IsNullOrEmpty(address)) return address;
                var idx = address.LastIndexOf(':');
                if (idx > 0 && idx < address.Length - 1)
                {
                    int port;
                    if (int.TryParse(address.Substring(idx + 1), out port)) return address.Substring(0, idx);
                }
                return address;
            }
        }

        public void RefreshConnectionInfo()
        {
            try
            {
                string host, user;
                RdpFileEditor.Read(FullPath, out host, out user);
                Host = host ?? "";
                User = user ?? "";
            }
            catch { }
        }

        public override string ToString()
        {
            return RelativeFolder == "(корінь)" ? DisplayName : RelativeFolder + " \\ " + DisplayName;
        }
    }

    // Вузол папки в дереві (Tag) -- для контекстного меню "пароль папки".
    public class RdpFolder
    {
        public string RelativePath;
    }

    public static class RdpScanner
    {
        public static string MakeRelative(string root, string fullPath)
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrEmpty(fullPath)) return null;
            root = root.TrimEnd('\\', '/') + "\\";
            return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? fullPath.Substring(root.Length) : null;
        }

        public static List<RdpEntry> Scan(string root)
        {
            var result = new List<RdpEntry>();
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return result;

            root = root.TrimEnd('\\', '/');
            var files = new List<string>();
            CollectFiles(root, files);

            foreach (var f in files)
            {
                var rel = f.Length > root.Length ? f.Substring(root.Length).TrimStart('\\', '/') : Path.GetFileName(f);
                var dir = Path.GetDirectoryName(rel);
                var entry = new RdpEntry
                {
                    FullPath = f,
                    DisplayName = Path.GetFileNameWithoutExtension(f),
                    RelativeFolder = string.IsNullOrEmpty(dir) ? "(корінь)" : dir,
                    RelativePath = rel,
                    FolderKey = dir ?? ""
                };
                entry.RefreshConnectionInfo();
                result.Add(entry);
            }

            return result
                .OrderBy(e => e.RelativeFolder, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        static void CollectFiles(string dir, List<string> results)
        {
            string[] files;
            try { files = Directory.GetFiles(dir, "*.rdp"); }
            catch { files = new string[0]; }
            results.AddRange(files);

            string[] subdirs;
            try { subdirs = Directory.GetDirectories(dir); }
            catch { subdirs = new string[0]; }

            foreach (var sd in subdirs)
            {
                CollectFiles(sd, results);
            }
        }
    }

    // Читання/запис адреси комп'ютера ("full address") і користувача ("username") в
    // оригінальному .rdp-файлі. Кодування файлу зберігається як було (mstsc зазвичай
    // пише UTF-16 з BOM, інші редактори -- UTF-8), решта рядків лишається без змін.
    public static class RdpFileEditor
    {
        const string AddressPrefix = "full address:s:";
        const string UserPrefix = "username:s:";

        static Encoding DetectEncoding(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return new UTF8Encoding(true);
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) return new UnicodeEncoding(false, true);
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) return new UnicodeEncoding(true, true);
            try
            {
                new UTF8Encoding(false, true).GetString(bytes);
                return new UTF8Encoding(false);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.Default;
            }
        }

        static List<string> ReadLines(string path, out Encoding encoding)
        {
            var bytes = File.ReadAllBytes(path);
            encoding = DetectEncoding(bytes);
            string text;
            using (var sr = new StreamReader(new MemoryStream(bytes), encoding, true))
            {
                text = sr.ReadToEnd();
            }
            var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
            if (lines.Count > 0 && lines[lines.Count - 1].Length == 0) lines.RemoveAt(lines.Count - 1);
            return lines;
        }

        public static void Read(string path, out string address, out string user)
        {
            Encoding enc;
            address = "";
            user = "";
            foreach (var line in ReadLines(path, out enc))
            {
                if (line.StartsWith(AddressPrefix, StringComparison.OrdinalIgnoreCase)) address = line.Substring(AddressPrefix.Length).Trim();
                else if (line.StartsWith(UserPrefix, StringComparison.OrdinalIgnoreCase)) user = line.Substring(UserPrefix.Length).Trim();
            }
        }

        public static void Save(string path, string address, string user)
        {
            Encoding enc;
            var lines = ReadLines(path, out enc);
            SetValue(lines, AddressPrefix, address);
            SetValue(lines, UserPrefix, user);
            File.WriteAllText(path, string.Join("\r\n", lines) + "\r\n", enc);
        }

        // Порожнє значення прибирає рядок (напр. "username:s:" без імені -- те саме, що
        // його відсутність); непорожнє -- замінює перший збіг, дублікати видаляє.
        static void SetValue(List<string> lines, string prefix, string value)
        {
            int first = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (!lines[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                if (first < 0) first = i;
                else { lines.RemoveAt(i); i--; }
            }

            if (string.IsNullOrEmpty(value))
            {
                if (first >= 0) lines.RemoveAt(first);
            }
            else if (first >= 0)
            {
                lines[first] = prefix + value;
            }
            else
            {
                lines.Add(prefix + value);
            }
        }
    }

    // Реальні іконки папки та .rdp-файлу з провідника Windows (той самий API, яким
    // користується сам Explorer) -- виглядають так само, як у файловому менеджері,
    // на відміну від будь-якого самостійно намальованого чи emoji-замінника.
    public static class ShellIcons
    {
        // SHGFI_USEFILEATTRIBUTES (іконка за розширенням/атрибутом без реального файлу)
        // на практиці повертає узагальнену "порожню" іконку замість справжньої
        // зареєстрованої -- тому іконки дістаються з РЕАЛЬНИХ шляхів: наявної теки
        // (%TEMP%) для папки, і щойно створеного порожнього *.rdp для RDP-файлу.
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr hIcon);

        [StructLayout(LayoutKind.Sequential)]
        struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
        }

        const uint SHGFI_ICON = 0x100;
        const uint SHGFI_SMALLICON = 0x1;

        public const int FolderImageIndex = 0;
        public const int RdpFileImageIndex = 1;

        static ImageList cached;

        public static ImageList GetFolderAndRdpImageList()
        {
            if (cached != null) return cached;

            var list = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
            list.Images.Add(GetFolderIconBitmap() ?? SystemIcons.Application.ToBitmap());
            list.Images.Add(GetRdpFileIconBitmap() ?? SystemIcons.Application.ToBitmap());

            cached = list;
            return cached;
        }

        static Bitmap GetFolderIconBitmap()
        {
            try
            {
                var shfi = new SHFILEINFO();
                IntPtr result = SHGetFileInfo(Path.GetTempPath(), 0, ref shfi, (uint)Marshal.SizeOf(shfi), SHGFI_ICON | SHGFI_SMALLICON);
                if (result == IntPtr.Zero || shfi.hIcon == IntPtr.Zero) return null;

                try
                {
                    using (var icon = Icon.FromHandle(shfi.hIcon))
                    {
                        // ToBitmap() копіює пікселі в незалежний Bitmap -- безпечно
                        // використовувати й після DestroyIcon нижче.
                        return (Bitmap)icon.ToBitmap().Clone();
                    }
                }
                finally
                {
                    DestroyIcon(shfi.hIcon);
                }
            }
            catch
            {
                return null;
            }
        }

        static Bitmap GetRdpFileIconBitmap()
        {
            try
            {
                var tempRdp = Path.Combine(Path.GetTempPath(), "RdpConsole_icon_probe.rdp");
                if (!File.Exists(tempRdp)) File.WriteAllText(tempRdp, "");

                using (var icon = Icon.ExtractAssociatedIcon(tempRdp))
                {
                    return icon != null ? (Bitmap)icon.ToBitmap().Clone() : null;
                }
            }
            catch
            {
                return null;
            }
        }
    }

    // Спільна побудова дерева тек/підключень (папки зверху, потім файли, за абеткою
    // на кожному рівні) -- використовується і в головному вікні, і у вікні швидкого
    // пошуку з трея, щоб не дублювати логіку.
    public static class RdpTreeBuilder
    {
        public static void Populate(TreeView tree, List<RdpEntry> entries, Func<RdpEntry, string> formatLabel,
            Func<string, string, string> formatFolder = null, List<RdpEntry> pinned = null)
        {
            // Перебудова дерева (після F5, зміни пароля, створення/переміщення файлу) не
            // повинна згортати вже розгорнуті папки й губити виділення -- запам'ятовуємо їх.
            var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectExpanded(tree.Nodes, expanded);
            var selectedKey = tree.SelectedNode != null ? NodeKey(tree.SelectedNode) : null;
            bool hadPinnedGroup = tree.Nodes.Cast<TreeNode>().Any(n => n.Tag == RdpPins.GroupTag);

            tree.BeginUpdate();
            tree.Nodes.Clear();

            // Група "Закріплені" -- першою; ті самі підключення лишаються й у своїх папках.
            // Нова група розгорнута; далі -- як її залишив користувач.
            if (pinned != null && pinned.Count > 0)
            {
                var group = new TreeNode(RdpPins.GroupTitle)
                {
                    ImageIndex = ShellIcons.FolderImageIndex,
                    SelectedImageIndex = ShellIcons.FolderImageIndex,
                    Tag = RdpPins.GroupTag
                };
                foreach (var entry in pinned)
                {
                    var text = formatLabel != null ? formatLabel(entry) : entry.DisplayName;
                    if (entry.FolderKey.Length > 0) text += "   (" + entry.FolderKey + ")";
                    group.Nodes.Add(new TreeNode(text)
                    {
                        ImageIndex = ShellIcons.RdpFileImageIndex,
                        SelectedImageIndex = ShellIcons.RdpFileImageIndex,
                        Tag = entry,
                        ToolTipText = entry.FullPath
                    });
                }
                tree.Nodes.Add(group);
                if (!hadPinnedGroup || expanded.Contains(PinnedGroupKey)) group.Expand();
            }

            var byFolder = new Dictionary<string, List<RdpEntry>>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                var key = entry.RelativeFolder == "(корінь)" ? "" : entry.RelativeFolder;
                List<RdpEntry> list;
                if (!byFolder.TryGetValue(key, out list))
                {
                    list = new List<RdpEntry>();
                    byFolder[key] = list;
                }
                list.Add(entry);
            }

            AddLevel(tree.Nodes, "", byFolder, formatLabel, formatFolder);
            RestoreState(tree, tree.Nodes, expanded, selectedKey);
            tree.EndUpdate();
        }

        const string PinnedGroupKey = "\0pinned";

        // Ключ вузла: "f:" + шлях папки або "e:" + шлях файлу (відносно кореневої папки).
        static string NodeKey(TreeNode node)
        {
            var folder = node.Tag as RdpFolder;
            if (folder != null) return "f:" + folder.RelativePath;
            var entry = node.Tag as RdpEntry;
            return entry != null ? "e:" + entry.RelativePath : null;
        }

        static void CollectExpanded(TreeNodeCollection nodes, HashSet<string> expanded)
        {
            foreach (TreeNode node in nodes)
            {
                if (!node.IsExpanded) continue;
                if (node.Tag == RdpPins.GroupTag) expanded.Add(PinnedGroupKey);
                var folder = node.Tag as RdpFolder;
                if (folder != null) expanded.Add(folder.RelativePath);
                CollectExpanded(node.Nodes, expanded);
            }
        }

        static void RestoreState(TreeView tree, TreeNodeCollection nodes, HashSet<string> expanded, string selectedKey)
        {
            foreach (TreeNode node in nodes)
            {
                var folder = node.Tag as RdpFolder;
                if (folder != null && expanded.Contains(folder.RelativePath)) node.Expand();
                if (selectedKey != null && string.Equals(NodeKey(node), selectedKey, StringComparison.OrdinalIgnoreCase))
                    tree.SelectedNode = node;
                RestoreState(tree, node.Nodes, expanded, selectedKey);
            }
        }

        // Розгорнути шлях до файлу й виділити його (напр., щойно створене підключення).
        public static void SelectEntry(TreeView tree, string relativePath)
        {
            var node = FindEntryNode(tree.Nodes, relativePath);
            if (node == null) return;
            node.EnsureVisible();
            tree.SelectedNode = node;
        }

        static TreeNode FindEntryNode(TreeNodeCollection nodes, string relativePath)
        {
            foreach (TreeNode node in nodes)
            {
                var entry = node.Tag as RdpEntry;
                if (entry != null && string.Equals(entry.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase)) return node;
                var found = FindEntryNode(node.Nodes, relativePath);
                if (found != null) return found;
            }
            return null;
        }

        static void AddLevel(TreeNodeCollection parentNodes, string currentPath, Dictionary<string, List<RdpEntry>> byFolder,
            Func<RdpEntry, string> formatLabel, Func<string, string, string> formatFolder)
        {
            var directSubfolders = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in byFolder.Keys)
            {
                if (key.Length == 0) continue;
                string rel;
                if (currentPath.Length == 0)
                {
                    rel = key;
                }
                else if (key.Length > currentPath.Length &&
                         key.StartsWith(currentPath, StringComparison.OrdinalIgnoreCase) &&
                         key[currentPath.Length] == '\\')
                {
                    rel = key.Substring(currentPath.Length + 1);
                }
                else
                {
                    continue;
                }

                var firstSegment = rel.Split('\\')[0];
                directSubfolders.Add(currentPath.Length == 0 ? firstSegment : currentPath + "\\" + firstSegment);
            }

            foreach (var subfolderPath in directSubfolders)
            {
                var name = subfolderPath.Substring(subfolderPath.LastIndexOf('\\') + 1);
                var node = new TreeNode(formatFolder != null ? formatFolder(subfolderPath, name) : name)
                {
                    ImageIndex = ShellIcons.FolderImageIndex,
                    SelectedImageIndex = ShellIcons.FolderImageIndex,
                    Tag = new RdpFolder { RelativePath = subfolderPath }
                };
                parentNodes.Add(node);
                AddLevel(node.Nodes, subfolderPath, byFolder, formatLabel, formatFolder);
            }

            List<RdpEntry> filesHere;
            if (byFolder.TryGetValue(currentPath, out filesHere))
            {
                foreach (var entry in filesHere.OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase))
                {
                    var text = formatLabel != null ? formatLabel(entry) : entry.DisplayName;
                    var node = new TreeNode(text)
                    {
                        ImageIndex = ShellIcons.RdpFileImageIndex,
                        SelectedImageIndex = ShellIcons.RdpFileImageIndex,
                        Tag = entry,
                        ToolTipText = entry.FullPath
                    };
                    parentNodes.Add(node);
                }
            }
        }
    }

    // Автозапуск разом із Windows -- через HKCU\...\Run (без прав адміністратора).
    // Джерело правди -- сам реєстр, а не settings.json: так стан чекбокса завжди
    // відповідає тому, що Windows реально запускатиме. Запуск іде з ключем
    // /minimized -- застосунок стартує одразу в треї, без головного вікна.
    public static class AutostartManager
    {
        const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "RdpConsole";
        public const string MinimizedArg = "/minimized";

        static string CommandLine
        {
            get { return "\"" + Application.ExecutablePath + "\" " + MinimizedArg; }
        }

        public static bool IsEnabled()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath))
                {
                    return key != null && key.GetValue(ValueName) != null;
                }
            }
            catch { return false; }
        }

        public static void SetEnabled(bool enabled)
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                if (enabled) key.SetValue(ValueName, CommandLine);
                else if (key.GetValue(ValueName) != null) key.DeleteValue(ValueName);
            }
        }

        // Якщо exe перенесли в іншу папку, запис у Run вказував би на старий шлях --
        // оновлюємо його при кожному запуску (лише коли автозапуск увімкнено).
        public static void RefreshPathIfEnabled()
        {
            try
            {
                if (!IsEnabled()) return;
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key != null && !string.Equals(key.GetValue(ValueName) as string, CommandLine, StringComparison.OrdinalIgnoreCase))
                        key.SetValue(ValueName, CommandLine);
                }
            }
            catch { }
        }
    }

    // Нове вікно попередження mstsc ("Увага: невідоме віддалене підключення", з усіма
    // перенаправленнями вимкненими за замовчуванням) з'явилось у квітневому оновленні
    // Windows 2026 (CVE-2026-26151) і показується при кожному відкритті непідписаного
    // .rdp. Microsoft документує тимчасовий відкат до попередньої поведінки через
    // політику RedirectionWarningDialogVersion = 1 у HKLM -- запис туди потребує прав
    // адміністратора, тому сам запис робимо через reg.exe з підвищенням (UAC).
    public static class RdpWarningPolicy
    {
        const string KeyPath = @"Software\Policies\Microsoft\Windows NT\Terminal Services\Client";
        const string ValueName = "RedirectionWarningDialogVersion";

        public static bool IsLegacyDialogEnabled()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(KeyPath))
                {
                    var v = key != null ? key.GetValue(ValueName) : null;
                    return v is int && (int)v == 1;
                }
            }
            catch { return false; }
        }

        // true -- прописати значення 1, false -- видалити значення (повернути нове вікно).
        // Повертає false, якщо користувач відмовився в UAC або reg.exe завершився з помилкою.
        public static bool SetLegacyDialog(bool enable)
        {
            var regKey = "\"HKLM\\" + KeyPath + "\"";
            var args = enable
                ? "add " + regKey + " /v " + ValueName + " /t REG_DWORD /d 1 /f"
                : "delete " + regKey + " /v " + ValueName + " /f";

            var psi = new ProcessStartInfo("reg.exe", args)
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };
            try
            {
                using (var proc = Process.Start(psi))
                {
                    proc.WaitForExit();
                    return proc.ExitCode == 0;
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false; // користувач натиснув "Ні" у вікні UAC
            }
        }
    }

    public static class Launcher
    {
        // Ці параметри відповідають чекбоксам у діалозі підтвердження RDP-з'єднання
        // ("Смарт-карти", "WebAuthn", "Буфер обміну", "Принтери") -- вмикаємо їх заздалегідь,
        // щоб діалог (коли він з'являється) відкривався з усіма позначеними пунктами.
        static readonly string[] ForceEnabledKeys = new[]
        {
            "redirectclipboard",
            "redirectprinters",
            "redirectsmartcards",
            "redirectwebauthn"
        };

        // Захист від повторного спрацювання: KeyDown автоповторюється, поки клавіша
        // (наприклад Enter) утримується, а e.Handled/e.SuppressKeyPress це не зупиняють --
        // тому той самий Connect міг викликатись двічі поспіль (і двічі копіювати той
        // самий пароль у буфер обміну). Ігноруємо повторний виклик з тими самими цілями,
        // якщо він стався одразу після попереднього.
        static DateTime lastConnectAt = DateTime.MinValue;
        static string lastConnectKey;

        public static void Connect(IWin32Window owner, AppSettings settings, IEnumerable<RdpEntry> targets)
        {
            var targetList = targets as IList<RdpEntry> ?? targets.ToList();
            if (targetList.Count == 0) return;

            var key = string.Join("|", targetList.Select(t => t.FullPath));
            var now = DateTime.UtcNow;
            if (key == lastConnectKey && (now - lastConnectAt).TotalMilliseconds < 800)
            {
                return;
            }
            lastConnectKey = key;
            lastConnectAt = now;

            foreach (var t in targetList)
            {
                try
                {
                    // Адресу/користувача могли змінити у файлі після сканування.
                    t.RefreshConnectionInfo();
                    var info = PasswordManager.Resolve(settings, t);
                    var pwd = info.Password;
                    bool credentialsReady = info.Source == PasswordSource.Windows;

                    // Опція "записувати в облікові дані Windows": пароль іде в Диспетчер
                    // облікових даних замість тимчасового файлу; далі для цього сервера
                    // діє пріоритет облікових даних Windows. Без користувача в .rdp запис
                    // неможливий -- тоді, як і без опції, пароль іде у файл.
                    if (!string.IsNullOrEmpty(pwd) && settings.StoreInWindowsCredentials &&
                        !string.IsNullOrEmpty(t.HostName) && !string.IsNullOrEmpty(t.User))
                    {
                        try
                        {
                            WindowsCredentialStore.Write(t.HostName, t.User, pwd);
                            pwd = null;
                            credentialsReady = true;
                        }
                        catch { }
                    }

                    var connectPath = PrepareConnectionFile(t.FullPath, pwd, credentialsReady);
                    var psi = new ProcessStartInfo("mstsc.exe", "\"" + connectPath + "\"");
                    psi.UseShellExecute = true;
                    Process.Start(psi);

                    if (!string.IsNullOrEmpty(pwd) && connectPath != t.FullPath)
                    {
                        ScheduleTempFileCleanup(connectPath);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(owner,
                        "Не вдалося підключитися до \"" + t.DisplayName + "\":\n" + ex.Message,
                        "Помилка підключення", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        // Шифрує пароль так само, як це робить сам mstsc.exe, коли зберігає "Дозволити
        // збереження облікових даних" у файл .rdp (DPAPI, прив'язка до поточного
        // облікового запису Windows, без додаткової ентропії -- інакше mstsc не зможе
        // розшифрувати назад). Отриманий рядок mstsc читає як "password 51:b:...".
        static string EncryptForMstsc(string plainPassword)
        {
            var bytes = Encoding.Unicode.GetBytes(plainPassword);
            var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            var sb = new StringBuilder(protectedBytes.Length * 2);
            foreach (var b in protectedBytes) sb.Append(b.ToString("X2"));
            return sb.ToString();
        }

        // System.Threading.Timer -- явно кваліфікуємо, бо в файлі також підключено
        // System.Windows.Forms, де є однойменний Timer.
        static readonly List<System.Threading.Timer> pendingCleanupTimers = new List<System.Threading.Timer>();

        // Тимчасова копія з паролем прибирається за кілька секунд після запуску mstsc
        // (файл потрібен лише в момент старту) -- щоб зашифрований пароль не лежав на
        // диску довше, ніж треба.
        static void ScheduleTempFileCleanup(string path)
        {
            System.Threading.Timer timer = null;
            timer = new System.Threading.Timer(_ =>
            {
                try { File.Delete(path); } catch { }
                lock (pendingCleanupTimers) { pendingCleanupTimers.Remove(timer); }
                timer.Dispose();
            }, null, TimeSpan.FromSeconds(20), Timeout.InfiniteTimeSpan);

            lock (pendingCleanupTimers) { pendingCleanupTimers.Add(timer); }
        }

        // Готує файл для запуску: копіює *.rdp у тимчасову теку (копія, створена нашим
        // процесом, не має позначки "з Інтернету" (Mark-of-the-Web), тому попередження
        // Windows "Невідомий видавець" для неї не з'являється -- на відміну від оригіналу,
        // який міг отримати цю позначку від хмарного синхронізатора (Google Drive тощо)),
        // примусово вмикає дозволи з ForceEnabledKeys і, якщо є збережений пароль, дописує
        // його як "password 51:b:..." -- той самий формат, який mstsc сам розуміє без
        // жодного запиту облікових даних. Оригінальний файл не змінюється.
        // credentialsReady -- облікові дані для сервера вже є в Windows: пароль у файл не
        // пишемо, але запит облікових даних так само вимикаємо.
        static string PrepareConnectionFile(string originalPath, string password, bool credentialsReady)
        {
            try
            {
                var lines = File.ReadAllLines(originalPath).ToList();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < lines.Count; i++)
                {
                    var idx = lines[i].IndexOf(':');
                    if (idx <= 0) continue;
                    var key = lines[i].Substring(0, idx).Trim();
                    foreach (var forceKey in ForceEnabledKeys)
                    {
                        if (string.Equals(key, forceKey, StringComparison.OrdinalIgnoreCase))
                        {
                            lines[i] = forceKey + ":i:1";
                            seen.Add(forceKey);
                        }
                    }
                }
                foreach (var forceKey in ForceEnabledKeys)
                {
                    if (!seen.Contains(forceKey)) lines.Add(forceKey + ":i:1");
                }

                if (!string.IsNullOrEmpty(password) || credentialsReady)
                {
                    // "prompt for credentials:i:1" (типове значення в багатьох .rdp) змушує
                    // mstsc показати діалог входу НАВІТЬ якщо пароль уже вбудовано у файл --
                    // діалог при цьому підставляє вбудований пароль крапками, і якщо потім ще
                    // вставити його з буфера обміну (Ctrl+V), він додається поверх, а не
                    // замінює, тож автентифікація падає з "подвоєним" паролем. Вимикаємо цей
                    // прапорець лише тоді, коли справді є що підставити замість запиту.
                    lines.RemoveAll(l =>
                        l.StartsWith("password 51:b:", StringComparison.OrdinalIgnoreCase) ||
                        l.StartsWith("prompt for credentials:", StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(password)) lines.Add("password 51:b:" + EncryptForMstsc(password));
                    lines.Add("prompt for credentials:i:0");
                }

                var tempDir = Path.Combine(Path.GetTempPath(), "RdpConsole");
                Directory.CreateDirectory(tempDir);
                var tempPath = Path.Combine(tempDir, Path.GetFileName(originalPath));
                File.WriteAllLines(tempPath, lines, new UTF8Encoding(false));
                return tempPath;
            }
            catch
            {
                // якщо щось пішло не так -- підключаємось напряму до оригінального файлу
                return originalPath;
            }
        }
    }

    // Перевірка й встановлення оновлень з GitHub -- за тим самим принципом, що й
    // ShadowSessionTool: version.txt у репозиторії звіряється з поточною версією,
    // завантажується готовий .exe з останнього релізу і підміняє поточний файл.
    public static class UpdateChecker
    {
        public const string VersionUrl = "https://raw.githubusercontent.com/Kolp305/RdpConsole/master/version.txt";
        public const string DownloadUrl = "https://github.com/Kolp305/RdpConsole/releases/latest/download/RdpConsole.exe";

        public static bool IsNewer(string remoteVersion, string currentVersion)
        {
            if (string.IsNullOrWhiteSpace(remoteVersion)) return false;
            Version rv, cv;
            if (!Version.TryParse(Normalize(remoteVersion), out rv)) return false;
            if (!Version.TryParse(Normalize(currentVersion), out cv)) return false;
            return rv > cv;
        }

        static string Normalize(string v)
        {
            return v.Trim().TrimStart('v', 'V');
        }

        // Готує й запускає службовий .bat, що дочекається завершення поточного процесу
        // (файл .exe не можна перезаписати, поки він запущений), підмінить його щойно
        // завантаженою версією і перезапустить застосунок. Викликати одразу перед виходом.
        public static void PrepareUpdateBatch(string newExePath)
        {
            var currentExe = Application.ExecutablePath;
            var pid = Process.GetCurrentProcess().Id;
            var batchPath = Path.Combine(Path.GetTempPath(), "RdpConsole_update_" + pid + ".bat");

            var script =
                "@echo off\r\n" +
                ":wait\r\n" +
                "tasklist /fi \"PID eq " + pid + "\" 2>nul | find \"" + pid + "\" >nul\r\n" +
                "if not errorlevel 1 (\r\n" +
                "  timeout /t 1 /nobreak >nul\r\n" +
                "  goto wait\r\n" +
                ")\r\n" +
                "copy /y \"" + newExePath + "\" \"" + currentExe + "\" >nul\r\n" +
                "del \"" + newExePath + "\" >nul\r\n" +
                "start \"\" \"" + currentExe + "\"\r\n" +
                "del \"%~f0\"\r\n";

            // ВАЖЛИВО: не Encoding.ASCII -- якщо шлях до застосунку містить кириличні
            // символи (наприклад, ім'я користувача Windows типу "Ярик"), ASCII замінює
            // їх на "?", і команди copy/start у .bat отримують зіпсований, неіснуючий
            // шлях. Encoding.Default -- поточна ANSI-кодова сторінка системи (напр.
            // Windows-1251) -- саме та, яку cmd.exe й так очікує для .bat за замовчуванням.
            File.WriteAllText(batchPath, script, Encoding.Default);

            var psi = new ProcessStartInfo("cmd.exe", "/c \"" + batchPath + "\"")
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(psi);
        }
    }

    // Закріплені підключення -- показуються першими: окрема група вгорі дерева,
    // на початку списку й результатів пошуку.
    public static class RdpPins
    {
        // Tag вузла-групи "Закріплені" в дереві.
        public static readonly object GroupTag = new object();
        public const string GroupTitle = "📌 Закріплені";

        public static bool IsPinned(AppSettings s, RdpEntry e)
        {
            return s.PinnedConnections.Any(p => string.Equals(p, e.RelativePath, StringComparison.OrdinalIgnoreCase));
        }

        public static List<RdpEntry> Pinned(AppSettings s, IEnumerable<RdpEntry> entries)
        {
            return entries.Where(e => IsPinned(s, e)).OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        // Закріплені -- першими, решта у вихідному порядку.
        public static List<RdpEntry> PinnedFirst(AppSettings s, IEnumerable<RdpEntry> entries)
        {
            var list = entries.ToList();
            return list.Where(e => IsPinned(s, e)).Concat(list.Where(e => !IsPinned(s, e))).ToList();
        }

        public static void Remove(AppSettings s, string relativePath)
        {
            s.PinnedConnections.RemoveAll(p => string.Equals(p, relativePath, StringComparison.OrdinalIgnoreCase));
        }

        // Файл перейменовано/переміщено через програму -- закріплення переїжджає разом із ним.
        public static void Rename(AppSettings s, string oldPath, string newPath)
        {
            if (!s.PinnedConnections.Any(p => string.Equals(p, oldPath, StringComparison.OrdinalIgnoreCase))) return;
            Remove(s, oldPath);
            s.PinnedConnections.Add(newPath);
        }

        // "Закріпити" / "Відкріпити" для одного чи кількох виділених підключень.
        public static void AddMenuItem(ToolStripItemCollection items, AppSettings s, IList<RdpEntry> entries, Action onChanged)
        {
            var list = entries.ToList();
            bool allPinned = list.Count > 0 && list.All(e => IsPinned(s, e));
            var mi = new ToolStripMenuItem(allPinned ? "Відкріпити" : "📌 Закріпити");
            mi.Click += (o, ev) =>
            {
                PasswordMenu.ChangeShared(s, () =>
                {
                    foreach (var e in list)
                    {
                        Remove(s, e.RelativePath);
                        if (!allPinned) s.PinnedConnections.Add(e.RelativePath);
                    }
                });
                onChanged();
            };
            items.Add(mi);
        }
    }

    // Пошук підключень -- спільний для головного вікна й вікна швидкого пошуку з трея.
    public static class RdpSearch
    {
        // Одні й ті самі фізичні клавіші в розкладках EN / УКР / РУС (рядок ` ... /) --
        // щоб знаходити підключення, навіть якщо запит набрано не в тій розкладці
        // ("ыкм" чи "ізм" -> "srv", "cthdth" -> "сервер").
        static readonly string[] Layouts =
        {
            "`qwertyuiop[]asdfghjkl;'zxcvbnm,./",
            "'йцукенгшщзхїфівапролджєячсмитьбю.",
            "ёйцукенгшщзхъфывапролджэячсмитьбю."
        };

        // Сам запит плюс його "переклад" з кожної розкладки в кожну іншу.
        public static List<string> QueryVariants(string query)
        {
            var q = query.ToLowerInvariant();
            var variants = new List<string> { q };
            foreach (var from in Layouts)
            {
                foreach (var to in Layouts)
                {
                    if (ReferenceEquals(from, to)) continue;
                    var sb = new StringBuilder(q.Length);
                    foreach (var c in q)
                    {
                        var idx = from.IndexOf(c);
                        sb.Append(idx >= 0 ? to[idx] : c);
                    }
                    var v = sb.ToString();
                    if (!variants.Contains(v)) variants.Add(v);
                }
            }
            return variants;
        }

        public static bool IsExcludedFolder(AppSettings s, string folderKey)
        {
            return !string.IsNullOrEmpty(folderKey) &&
                   s.SearchExcludedFolders.Any(f => string.Equals(f, folderKey, StringComparison.OrdinalIgnoreCase));
        }

        // Файл у папці "не бере участі в пошуку" або в будь-якій її підпапці.
        public static bool IsExcluded(AppSettings s, RdpEntry e)
        {
            foreach (var f in s.SearchExcludedFolders)
            {
                if (string.Equals(e.FolderKey, f, StringComparison.OrdinalIgnoreCase) ||
                    e.FolderKey.StartsWith(f + "\\", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static IEnumerable<RdpEntry> Filter(AppSettings s, IEnumerable<RdpEntry> entries, string filter)
        {
            if (filter.Length == 0) return entries;
            var variants = QueryVariants(filter);
            return entries.Where(x => !IsExcluded(s, x) && variants.Any(v =>
                x.DisplayName.IndexOf(v, StringComparison.OrdinalIgnoreCase) >= 0 ||
                x.RelativeFolder.IndexOf(v, StringComparison.OrdinalIgnoreCase) >= 0));
        }
    }

    // Пункти контекстного меню для паролів і підписи з мітками джерела пароля --
    // спільні для головного вікна та вікна швидкого пошуку з трея.
    public static class PasswordMenu
    {
        // Підпис файлу в дереві/списку з міткою джерела пароля (W / ✓ / 📁 / •).
        public static string EntryLabel(AppSettings s, RdpEntry entry, string text)
        {
            var marker = PasswordManager.Resolve(s, entry).Marker;
            return marker.Length == 0 ? text : text + "   " + marker;
        }

        // Мітка папки з власним паролем у дереві.
        public static string FolderLabel(AppSettings s, string folderKey, string name)
        {
            if (HasFolderPassword(s, folderKey)) name += "   🔑";
            if (RdpSearch.IsExcludedFolder(s, folderKey)) name += "   (без пошуку)";
            return name;
        }

        // Перемикач "Не бере участь у пошуку" для папки в дереві.
        public static void AddFolderSearchItem(ToolStripItemCollection items, AppSettings s, string folderKey, Action onChanged)
        {
            bool excluded = RdpSearch.IsExcludedFolder(s, folderKey);
            var mi = new ToolStripMenuItem("Не бере участь у пошуку") { Checked = excluded };
            mi.Click += (o, ev) => Change(s, () =>
            {
                if (excluded) s.SearchExcludedFolders.RemoveAll(f => string.Equals(f, folderKey, StringComparison.OrdinalIgnoreCase));
                else s.SearchExcludedFolders.Add(folderKey);
            }, onChanged);
            items.Add(new ToolStripSeparator());
            items.Add(mi);
        }

        public static bool HasFolderPassword(AppSettings s, string folderKey)
        {
            return !string.IsNullOrEmpty(folderKey) && s.FolderPasswords.ContainsKey(folderKey);
        }

        // runModal -- обгортка для діалогів (вікно швидкого пошуку на цей час вимикає
        // автозакриття при втраті фокуса); onChanged -- оновити відображення.
        public static void AddEntryItems(ToolStripItemCollection items, IWin32Window owner, AppSettings s, RdpEntry t,
            Action<Action> runModal, Action onChanged)
        {
            bool hasOwn = s.FilePasswords.ContainsKey(t.RelativePath);

            var miPwd = new ToolStripMenuItem(hasOwn ? "Змінити пароль підключення..." : "Зберегти пароль підключення...");
            miPwd.Click += (o, ev) => runModal(() =>
            {
                using (var dlg = new PasswordDialog("Пароль для \"" + t.DisplayName + "\"", s.Theme))
                {
                    if (dlg.ShowDialog(owner) != DialogResult.OK) return;
                    t.RefreshConnectionInfo();
                    Change(s, () => PasswordManager.SetFilePassword(s, t, dlg.Password), onChanged);
                }
            });
            items.Add(miPwd);

            if (hasOwn)
            {
                var miDel = new ToolStripMenuItem("Видалити пароль підключення");
                miDel.Click += (o, ev) => Change(s, () => s.FilePasswords.Remove(t.RelativePath), onChanged);
                items.Add(miDel);
            }

            if (t.FolderKey.Length > 0)
            {
                AddFolderItems(items, owner, s, t.FolderKey, true, runModal, onChanged);
            }

            // Облікові дані Windows мають найвищий пріоритет, тож застарілий запис у
            // Диспетчері облікових даних перекриває паролі застосунку -- його можна прибрати.
            if (WindowsCredentialStore.HasStoredCredential(t.HostName))
            {
                var miDelWin = new ToolStripMenuItem("Видалити облікові дані Windows (TERMSRV/" + t.HostName + ")");
                miDelWin.Click += (o, ev) => runModal(() =>
                {
                    var r = MessageBox.Show(owner,
                        "Видалити збережені облікові дані Windows для \"" + t.HostName + "\"?\n\n" +
                        "Після цього при підключенні діятиме пароль із застосунку (якщо він є).",
                        "Облікові дані Windows", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (r != DialogResult.Yes) return;
                    WindowsCredentialStore.Delete(t.HostName);
                    onChanged();
                });
                items.Add(miDelWin);
            }
        }

        // Пункти "пароль папки": на самій папці в дереві або (withFolderName) у меню файлу --
        // для папки, де цей файл лежить.
        public static void AddFolderItems(ToolStripItemCollection items, IWin32Window owner, AppSettings s, string folderKey,
            bool withFolderName, Action<Action> runModal, Action onChanged)
        {
            var suffix = withFolderName ? " \"" + folderKey + "\"" : "";
            bool has = HasFolderPassword(s, folderKey);

            var miSet = new ToolStripMenuItem((has ? "Змінити пароль папки" : "Зберегти пароль папки") + suffix + "...");
            miSet.Click += (o, ev) => runModal(() =>
            {
                using (var dlg = new PasswordDialog("Пароль папки \"" + folderKey + "\"", s.Theme))
                {
                    if (dlg.ShowDialog(owner) != DialogResult.OK) return;
                    Change(s, () => s.FolderPasswords[folderKey] = dlg.Password, onChanged);
                }
            });
            items.Add(miSet);

            if (has)
            {
                var miDel = new ToolStripMenuItem("Видалити пароль папки" + suffix);
                miDel.Click += (o, ev) => Change(s, () => s.FolderPasswords.Remove(folderKey), onChanged);
                items.Add(miDel);
            }
        }

        static void Change(AppSettings s, Action change, Action onChanged)
        {
            ChangeShared(s, change);
            onChanged();
        }

        // Зміна спільних налаштувань: спершу підтягуємо свіжу версію файлу (щоб не затерти
        // зміни, зроблені на іншому ПК), потім застосовуємо зміну й зберігаємо.
        public static void ChangeShared(AppSettings s, Action change)
        {
            SettingsManager.ReloadShared(s, true);
            change();
            SettingsManager.SaveShared(s);
        }
    }

    public class MainForm : Form
    {
        AppSettings settings;
        List<RdpEntry> allEntries = new List<RdpEntry>();

        TextBox txtSearch;
        Button btnExit;
        Button btnSettings;
        Button btnRefresh;
        Button btnViewMode;
        Button btnRepo;
        Panel topPanel;
        bool micaEnabled;
        LinkLabel lnkVersion;
        ListView listView;
        TreeView treeView;
        StatusStrip statusStrip;
        ToolStripStatusLabel statusLabel;
        ContextMenuStrip itemMenu;

        NotifyIcon trayIcon;
        bool reallyExit;
        bool trayHintShown;
        bool startHidden;
        bool loaded;
        // Наступне відкриття контекстного меню -- для порожнього місця списку/дерева.
        bool menuOnEmptySpace;

        // Глобальна гаряча клавіша (за замовчуванням Ctrl+Alt+R, змінюється в
        // Налаштуваннях) -- відкриває вікно швидкого пошуку з будь-якого місця в
        // Windows, навіть коли застосунок згорнутий у трей.
        const int HOTKEY_ID = 0xB105;
        const int WM_HOTKEY = 0x0312;
        bool hotkeyRegistered;

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // Windows забороняє фоновим процесам самовільно забирати фокус (SetForegroundWindow
        // мовчки не спрацьовує), тому вікно, відкрите по глобальній гарячій клавіші, могло
        // з'являтися без фокуса -- і миттєво закриватися через Deactivate. Обхід: на час
        // активації приєднуємо свій потік введення до потоку поточного активного вікна
        // (AttachThreadInput) -- тоді SetForegroundWindow дозволено.
        // Раніше тут імітувалося натискання Alt, але "відпускання" Alt приходило вже в
        // наше вікно й вмикало режим меню вікна -- перша набрана буква пошуку губилась.
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

        [DllImport("user32.dll")]
        static extern bool BringWindowToTop(IntPtr hWnd);

        static void ForceForegroundWindow(IntPtr hWnd)
        {
            var foreground = GetForegroundWindow();
            uint foregroundThread = foreground != IntPtr.Zero ? GetWindowThreadProcessId(foreground, IntPtr.Zero) : 0;
            uint currentThread = GetCurrentThreadId();
            bool attached = foregroundThread != 0 && foregroundThread != currentThread &&
                            AttachThreadInput(currentThread, foregroundThread, true);
            try
            {
                BringWindowToTop(hWnd);
                SetForegroundWindow(hWnd);
            }
            finally
            {
                if (attached) AttachThreadInput(currentThread, foregroundThread, false);
            }
        }

        public MainForm(bool startHidden = false)
        {
            this.startHidden = startHidden;

            // Форма побудована вручну (без дизайнера), координати -- у реальних пікселях;
            // вимикаємо автомасштабування WinForms, щоб уникнути артефактів промальовування
            // (обрізані рамки, елементи керування за межами вікна) при нестандартному DPI.
            AutoScaleMode = AutoScaleMode.None;

            settings = SettingsManager.Load();

            Text = "Консоль підключення по RDP";
            Font = new Font("Segoe UI", 9.5f);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            StartPosition = FormStartPosition.CenterScreen;
            Width = settings.WindowWidth > 0 ? settings.WindowWidth : 940;
            Height = settings.WindowHeight > 0 ? settings.WindowHeight : 620;
            MinimumSize = new Size(900, 380);
            KeyPreview = true;

            BuildUi();
            BuildTrayIcon();

            Load += MainForm_Load;
            FormClosing += MainForm_FormClosing;
            KeyDown += MainForm_KeyDown;
            Resize += MainForm_Resize;
        }

        // Іконка в треї -- завжди присутня, поки застосунок запущено (навіть коли
        // головне вікно приховане). ЛКМ -- відкрити/показати вікно; ПКМ -- одразу
        // відкриває вікно швидкого пошуку (без проміжного контекстного меню).
        void BuildTrayIcon()
        {
            trayIcon = new NotifyIcon
            {
                Icon = Icon,
                Text = "Консоль підключення по RDP",
                Visible = true
            };
            trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) ShowMainWindow();
                else if (e.Button == MouseButtons.Right) OpenTraySearch();
            };
            trayIcon.DoubleClick += (s, e) => ShowMainWindow();
        }

        // Відкриває окреме легке вікно швидкого пошуку біля курсора.
        void OpenTraySearch()
        {
            RefreshIfSharedSettingsChanged();
            var form = new TraySearchForm(allEntries, settings, this, () => ExitApplication(),
                () => { RescanAndFill(); return allEntries; });
            form.FormClosed += (s, e) => { if (form.PasswordsChanged && !IsDisposed) ApplyFilter(); };

            var screen = Screen.FromPoint(Cursor.Position).WorkingArea;
            int x = Math.Min(Cursor.Position.X, screen.Right - form.Width - 4);
            int y = Cursor.Position.Y - form.Height - 4;
            if (y < screen.Top) y = Math.Min(Cursor.Position.Y + 4, screen.Bottom - form.Height - 4);
            x = Math.Max(screen.Left, x);
            y = Math.Max(screen.Top, y);

            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(x, y);
            form.Show();
            ForceForegroundWindow(form.Handle);
            form.Activate();
            form.FocusSearchBox();
        }

        void ShowMainWindow()
        {
            // ВАЖЛИВО: не чіпати ShowInTaskbar тут -- WinForms перестворює handle вікна
            // (RecreateHandle) щоразу, коли ця властивість змінюється на вже створеному
            // вікні, а RegisterHotKey прив'язаний саме до конкретного handle. Через це
            // глобальна гаряча клавіша переставала працювати одразу після першого
            // згортання в трей. Прихований/показаний через Hide()/Show() формі й так не
            // показує кнопку на панелі задач, тож окремо керувати ShowInTaskbar не треба.
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            RefreshIfSharedSettingsChanged();
        }

        // Спільний файл налаштувань міг змінитися з іншого ПК (Google Drive) -- тоді
        // перечитуємо його й оновлюємо список, тему та гарячу клавішу.
        void RefreshIfSharedSettingsChanged()
        {
            uint oldMods = settings.HotkeyModifiers, oldKey = settings.HotkeyKey;
            if (!SettingsManager.ReloadShared(settings, true)) return;

            ApplyFilter();
            ApplyTheme();
            if (oldMods != settings.HotkeyModifiers || oldKey != settings.HotkeyKey)
            {
                UnregisterGlobalHotkey();
                RegisterGlobalHotkey();
            }
        }

        // Зміна спільних налаштувань: спершу підтягуємо свіжу версію файлу (щоб не затерти
        // зміни, зроблені на іншому ПК), потім застосовуємо зміну й зберігаємо.
        void UpdateShared(Action change)
        {
            SettingsManager.ReloadShared(settings, true);
            change();
            SettingsManager.SaveShared(settings);
            ApplyFilter();
        }

        void ExitApplication()
        {
            reallyExit = true;
            Close();
        }

        void MainForm_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                MinimizeToTray();
            }
        }

        void MinimizeToTray()
        {
            Hide();

            if (!trayHintShown)
            {
                trayHintShown = true;
                try
                {
                    trayIcon.ShowBalloonTip(2000, "RDP Console",
                        "Застосунок згорнуто в трей. Клацніть на іконку, щоб відкрити вікно знову.",
                        ToolTipIcon.Info);
                }
                catch { }
            }
        }

        void BuildUi()
        {
            topPanel = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8, 8, 8, 6) };

            // Стиль версії/посилання на репозиторій -- як у ShadowSessionTool: маленька
            // намальована іконка-ланцюжок (а не емодзі, яке по-різному рендериться) і
            // підкреслений текст версії, що виглядає як гіперпосилання.
            btnRepo = new Button
            {
                Dock = DockStyle.Left,
                Width = 26,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Tag = "iconOnly",
                Image = CreateLinkIcon(SystemColors.GrayText),
                ImageAlign = ContentAlignment.MiddleCenter
            };
            btnRepo.FlatAppearance.BorderSize = 0;
            btnRepo.Click += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo(Program.RepoUrl) { UseShellExecute = true }); }
                catch { }
            };
            var repoToolTip = new ToolTip();
            repoToolTip.SetToolTip(btnRepo, "Відкрити репозиторій на GitHub");

            lnkVersion = new LinkLabel
            {
                Dock = DockStyle.Left,
                Width = 70,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "v" + Program.AppVersion,
                LinkColor = SystemColors.GrayText,
                LinkBehavior = LinkBehavior.AlwaysUnderline
            };
            lnkVersion.LinkClicked += (s, e) =>
            {
                if (pendingUpdateVersion != null) PromptUpdate(pendingUpdateVersion);
                else CheckForUpdates(true);
            };

            txtSearch = new TextBox { Dock = DockStyle.Left, Width = 320, Margin = new Padding(0, 0, 10, 0) };
            SetPlaceholder(txtSearch, "Пошук підключення...");
            txtSearch.TextChanged += (s, e) => { ClearPlaceholderState(); ApplyFilter(); };
            txtSearch.KeyDown += TxtSearch_KeyDown;

            btnRefresh = new Button { Dock = DockStyle.Right, Width = 100, Text = "Оновити (F5)", Margin = new Padding(4, 0, 0, 0) };
            btnRefresh.Click += (s, e) => RescanAndFill();

            btnSettings = new Button { Dock = DockStyle.Right, Width = 110, Text = "Налаштування", Margin = new Padding(4, 0, 0, 0) };
            btnSettings.Click += (s, e) => OpenSettings();

            btnExit = new Button { Dock = DockStyle.Right, Width = 150, Text = "Завершити програму", Margin = new Padding(4, 0, 0, 0) };
            btnExit.Click += (s, e) => ExitApplication();

            btnViewMode = new Button { Dock = DockStyle.Right, Width = 100, Margin = new Padding(4, 0, 0, 0) };
            btnViewMode.Click += (s, e) => UpdateShared(() => settings.HierarchyView = !settings.HierarchyView);

            topPanel.Controls.Add(txtSearch);
            topPanel.Controls.Add(lnkVersion);
            topPanel.Controls.Add(btnRepo);
            topPanel.Controls.Add(btnExit);
            topPanel.Controls.Add(btnSettings);
            topPanel.Controls.Add(btnRefresh);
            topPanel.Controls.Add(btnViewMode);

            listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                MultiSelect = true,
                GridLines = true,
                ShowGroups = true,
                ShowItemToolTips = true
            };
            listView.Columns.Add("Назва підключення", 700);
            listView.Columns.Add("Пароль", 90);
            listView.MouseDoubleClick += ListView_MouseDoubleClick;
            listView.MouseUp += ListView_MouseUp;
            listView.KeyDown += ListView_KeyDown;
            listView.SizeChanged += (s, e) =>
            {
                if (listView.Columns.Count > 1)
                    listView.Columns[0].Width = Math.Max(200, listView.ClientSize.Width - listView.Columns[1].Width - 4);
            };

            // Ієрархічний перегляд (папки зверху, вкладеність через розгортання) --
            // використовується лише коли пошук порожній і увімкнено режим "Ієрархія".
            // Під час пошуку завжди показується плаский список (listView) незалежно
            // від цього перемикача.
            treeView = new TreeView
            {
                Dock = DockStyle.Fill,
                HideSelection = false,
                ShowNodeToolTips = true,
                Visible = false,
                ImageList = ShellIcons.GetFolderAndRdpImageList()
            };
            treeView.NodeMouseDoubleClick += (s, e) =>
            {
                treeView.SelectedNode = e.Node;
                if (e.Node.Tag is RdpEntry) ConnectSelected();
            };
            treeView.NodeMouseClick += (s, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                treeView.SelectedNode = e.Node;
                if (e.Node.Tag is RdpEntry || e.Node.Tag is RdpFolder) itemMenu.Show(treeView, e.Location);
            };
            treeView.MouseUp += (s, e) =>
            {
                if (e.Button != MouseButtons.Right || treeView.GetNodeAt(e.Location) != null) return;
                menuOnEmptySpace = true;
                itemMenu.Show(treeView, e.Location);
            };
            treeView.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Delete)
                {
                    DeleteSelected();
                    e.Handled = true;
                    return;
                }
                if (e.KeyCode != Keys.Enter) return;
                var node = treeView.SelectedNode;
                if (node == null) return;
                if (node.Tag is RdpEntry) ConnectSelected();
                else node.Toggle();
                e.Handled = true;
            };

            itemMenu = new ContextMenuStrip();
            itemMenu.Opening += ItemMenu_Opening;

            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            statusStrip.Items.Add(statusLabel);

            Controls.Add(treeView);
            Controls.Add(listView);
            Controls.Add(topPanel);
            Controls.Add(statusStrip);

            ApplyTheme();
        }

        void ApplyTheme()
        {
            ThemeManager.Apply(this, settings.Theme);
            var c = ThemeManager.Get(settings.Theme);
            btnRepo.Image = CreateLinkIcon(c.AccentLink);
            lnkVersion.LinkColor = c.AccentLink;
            searchNormalColor = c.ControlFore;
            txtSearch.ForeColor = searchShowingPlaceholder ? PlaceholderColor : searchNormalColor;
            if (IsHandleCreated) ApplyMica();
        }

        // Windows 11 Mica -- напівпрозорий розмитий фон верхньої панелі й статус-рядка
        // (як у застосунках Параметрів). На старіших системах DwmMica.TryEnable
        // поверне false, і форма просто лишиться зі звичайним непрозорим фоном теми.
        void ApplyMica()
        {
            if (settings.MicaEnabled)
            {
                micaEnabled = DwmMica.TryEnable(Handle, settings.Theme == ThemeManager.DarkThemeName);
            }
            else if (micaEnabled)
            {
                DwmMica.Disable(Handle);
                micaEnabled = false;
            }

            var c = ThemeManager.Get(settings.Theme);
            if (micaEnabled)
            {
                topPanel.BackColor = Color.Black;
                statusStrip.BackColor = Color.Black;
                statusLabel.ForeColor = Color.FromArgb(235, 235, 235);
                lnkVersion.BackColor = Color.Transparent;
                lnkVersion.LinkColor = Color.FromArgb(210, 210, 210);
                btnRepo.BackColor = Color.Transparent;
                btnRepo.Image = CreateLinkIcon(Color.FromArgb(210, 210, 210));
            }
            else
            {
                topPanel.BackColor = c.PanelBack;
                statusStrip.BackColor = c.PanelBack;
                statusLabel.ForeColor = c.Foreground;
                lnkVersion.BackColor = Color.Transparent;
                lnkVersion.LinkColor = c.AccentLink;
                btnRepo.BackColor = c.PanelBack;
                btnRepo.Image = CreateLinkIcon(c.AccentLink);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyMica();
        }

        // ---- Placeholder helper для TextBox (WinForms не має вбудованого) ----
        static readonly Color PlaceholderColor = SystemColors.GrayText;
        Color searchNormalColor;
        string searchPlaceholderText;
        bool searchShowingPlaceholder;

        void SetPlaceholder(TextBox box, string text)
        {
            searchNormalColor = box.ForeColor;
            searchPlaceholderText = text;
            box.GotFocus += (s, e) => RemovePlaceholder();
            box.LostFocus += (s, e) => ApplyPlaceholderIfEmpty();
            ApplyPlaceholderIfEmpty();
        }

        void ApplyPlaceholderIfEmpty()
        {
            if (txtSearch.Text.Length == 0)
            {
                searchShowingPlaceholder = true;
                txtSearch.Text = searchPlaceholderText;
                txtSearch.ForeColor = PlaceholderColor;
            }
        }

        void RemovePlaceholder()
        {
            if (searchShowingPlaceholder)
            {
                searchShowingPlaceholder = false;
                txtSearch.Text = "";
                txtSearch.ForeColor = searchNormalColor;
            }
        }

        void ClearPlaceholderState()
        {
            if (!txtSearch.Focused) return;
            searchShowingPlaceholder = false;
            txtSearch.ForeColor = searchNormalColor;
        }

        string CurrentSearchText
        {
            get { return searchShowingPlaceholder ? "" : txtSearch.Text; }
        }

        // Старт згорнутим у трей (/minimized, автозапуск): Application.Run показує форму,
        // тож перший показ перехоплюємо тут. Handle створюємо явно -- він потрібен для
        // RegisterHotKey і прийому WM_SHOWME, а подія Load без показу вікна не настане,
        // тому ініціалізацію викликаємо вручну (повторно вона не виконається, див. loaded).
        protected override void SetVisibleCore(bool value)
        {
            if (startHidden && value)
            {
                startHidden = false;
                if (!IsHandleCreated) CreateHandle();
                base.SetVisibleCore(false);
                MainForm_Load(this, EventArgs.Empty);
                return;
            }
            base.SetVisibleCore(value);
        }

        void MainForm_Load(object sender, EventArgs e)
        {
            if (loaded) return;
            loaded = true;
            AutostartManager.RefreshPathIfEnabled();
            if (SettingsManager.LoadError != null)
            {
                MessageBox.Show(this, SettingsManager.LoadError, "Консоль підключення по RDP",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            EnsureRootFolder();
            RescanAndFill();
            RegisterGlobalHotkey();
            CheckForUpdates(false);
        }

        string pendingUpdateVersion;

        // Малює просту іконку-ланцюжок (2 дуги + перемичка), як у ShadowSessionTool --
        // надійніше й чіткіше за emoji-символ, який по-різному рендериться в списках.
        static Bitmap CreateLinkIcon(Color color)
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var pen = new Pen(color, 2f))
                {
                    pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    g.DrawArc(pen, 1, 5, 7, 7, 90, 180);
                    g.DrawArc(pen, 8, 4, 7, 7, 270, 180);
                    g.DrawLine(pen, 5, 8, 11, 8);
                }
            }
            return bmp;
        }

        // manual=false -- тиха фонова перевірка при запуску: якщо є новіша версія,
        // просто підсвічує посилання (без спливаючого вікна), як задумано.
        // manual=true -- клік по посиланню: завжди показує результат діалогом
        // (пропозицію оновитися або підтвердження, що версія вже остання).
        void CheckForUpdates(bool manual)
        {
            var originalText = lnkVersion.Text;
            lnkVersion.Enabled = false;
            if (manual) lnkVersion.Text = "Перевірка...";

            var thread = new Thread(() =>
            {
                string latest = null;
                Exception error = null;
                try
                {
                    // ВАЖЛИВО: виставляти TLS 1.2 треба саме тут, безпосередньо перед
                    // запитом. У статичному конструкторі UpdateChecker це НЕ спрацьовувало:
                    // звернення лише до const-полів (VersionUrl/DownloadUrl) не запускає
                    // статичний конструктор типу (const-и вбудовуються компілятором),
                    // тож ServicePointManager.SecurityProtocol так і лишався типовим і
                    // запит падав з "Не удалось создать защищенный канал SSL/TLS".
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    using (var wc = new WebClient())
                    {
                        wc.Headers.Add("User-Agent", "RdpConsole");
                        latest = wc.DownloadString(UpdateChecker.VersionUrl).Trim();
                    }
                }
                catch (Exception ex) { error = ex; }

                if (IsDisposed) return;
                try { BeginInvoke(new Action(() => OnUpdateCheckResult(latest, error, manual, originalText))); }
                catch { }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        void OnUpdateCheckResult(string latestVersion, Exception error, bool manual, string originalText)
        {
            lnkVersion.Enabled = true;

            if (error != null)
            {
                lnkVersion.Text = originalText;
                if (manual)
                {
                    MessageBox.Show(this, "Не вдалося перевірити оновлення:\n" + error.Message,
                        "RDP Console", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return;
            }

            bool isNewer = UpdateChecker.IsNewer(latestVersion, Program.AppVersion);
            if (isNewer)
            {
                pendingUpdateVersion = latestVersion;
                lnkVersion.Text = "v" + Program.AppVersion + " ↑";
                lnkVersion.LinkColor = Color.OrangeRed;

                if (manual) PromptUpdate(latestVersion);
            }
            else
            {
                pendingUpdateVersion = null;
                lnkVersion.Text = "v" + Program.AppVersion;
                lnkVersion.LinkColor = SystemColors.GrayText;
                if (manual)
                {
                    MessageBox.Show(this, "У вас уже остання версія (" + Program.AppVersion + ").",
                        "RDP Console", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        void PromptUpdate(string newVersion)
        {
            var r = MessageBox.Show(this,
                "Доступна нова версія " + newVersion + " (поточна: " + Program.AppVersion + ").\n\n" +
                "Завантажити й оновити зараз? Застосунок перезапуститься автоматично.",
                "Оновлення RDP Console", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (r == DialogResult.Yes) StartDownloadAndInstall();
        }

        void StartDownloadAndInstall()
        {
            lnkVersion.Enabled = false;
            lnkVersion.Text = "Завантаження...";

            var thread = new Thread(() =>
            {
                Exception error = null;
                string tempPath = Path.Combine(Path.GetTempPath(), "RdpConsole_update_" + Guid.NewGuid().ToString("N") + ".exe");
                try
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    using (var wc = new WebClient())
                    {
                        wc.Headers.Add("User-Agent", "RdpConsole");
                        wc.DownloadFile(UpdateChecker.DownloadUrl, tempPath);
                    }
                }
                catch (Exception ex) { error = ex; }

                if (IsDisposed) return;
                try { BeginInvoke(new Action(() => OnDownloadComplete(tempPath, error))); }
                catch { }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        void OnDownloadComplete(string tempPath, Exception error)
        {
            if (error != null)
            {
                lnkVersion.Enabled = true;
                lnkVersion.Text = "v" + Program.AppVersion + " ↑";
                MessageBox.Show(this, "Не вдалося завантажити оновлення:\n" + error.Message,
                    "RDP Console", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            UpdateChecker.PrepareUpdateBatch(tempPath);
            ExitApplication();
        }

        void RegisterGlobalHotkey()
        {
            if (settings.HotkeyKey == 0)
            {
                // Користувач явно вимкнув гарячу клавішу в Налаштуваннях.
                trayIcon.Text = "Консоль підключення по RDP  •  гаряча клавіша вимкнена";
                return;
            }

            hotkeyRegistered = RegisterHotKey(Handle, HOTKEY_ID, settings.HotkeyModifiers, settings.HotkeyKey);

            // Спливаючі повідомлення трею на Windows 11 часто не показуються (залежить від
            // налаштувань сповіщень/фокусування), тому статус гарячої клавіші додатково
            // видно в будь-який момент через підказку іконки в треї (наведення мишею), а
            // про невдалу реєстрацію одразу повідомляємо надійним MessageBox, а не лише
            // (ненадійною) бульбашкою.
            var combo = HotkeyUtil.Format(settings.HotkeyModifiers, settings.HotkeyKey);
            trayIcon.Text = "Консоль підключення по RDP" +
                (hotkeyRegistered ? "  •  " + combo : "  •  " + combo + " неактивна");

            if (!hotkeyRegistered)
            {
                int errorCode = Marshal.GetLastWin32Error();
                MessageBox.Show(this,
                    "Не вдалося зареєструвати глобальну гарячу клавішу " + combo + " " +
                    "(код помилки Windows: " + errorCode + "). " +
                    "Ймовірно, цю комбінацію вже використовує інша програма.\n\n" +
                    "Застосунок продовжує працювати, просто ця клавіша не спрацьовуватиме. " +
                    "Можна обрати іншу комбінацію в Налаштуваннях.",
                    "RDP Console", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void UnregisterGlobalHotkey()
        {
            if (hotkeyRegistered)
            {
                UnregisterHotKey(Handle, HOTKEY_ID);
                hotkeyRegistered = false;
            }
        }

        void EnsureRootFolder()
        {
            if (!string.IsNullOrWhiteSpace(settings.RootFolder) && Directory.Exists(settings.RootFolder))
                return;

            MessageBox.Show(this,
                "Вкажіть папку, у якій зберігаються файли підключень (*.rdp).",
                "Консоль підключення по RDP", MessageBoxButtons.OK, MessageBoxIcon.Information);

            using (var fbd = new FolderBrowserDialog { Description = "Оберіть папку з файлами *.rdp" })
            {
                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    settings.RootFolder = fbd.SelectedPath;
                    SettingsManager.SaveLocal(settings);
                }
            }
        }

        void RescanAndFill()
        {
            SettingsManager.ReloadShared(settings, true);
            WindowsCredentialStore.ClearCache();
            allEntries = RdpScanner.Scan(settings.RootFolder);
            if (PasswordManager.Reconcile(settings, allEntries)) SettingsManager.SaveShared(settings);
            ApplyFilter();
        }

        void ApplyFilter()
        {
            var filter = CurrentSearchText.Trim();
            btnViewMode.Text = settings.HierarchyView ? "☰ Список" : "🌲 Ієрархія";
            btnViewMode.Enabled = filter.Length == 0;

            // Під час пошуку -- завжди плаский список, незалежно від перемикача.
            bool useHierarchy = filter.Length == 0 && settings.HierarchyView;

            treeView.Visible = useHierarchy;
            listView.Visible = !useHierarchy;

            int shown;
            if (useHierarchy)
            {
                shown = BuildHierarchyTree();
            }
            else
            {
                shown = BuildFlatList(filter);
            }

            var where = string.IsNullOrWhiteSpace(settings.RootFolder) ? "(папку не вибрано)" : settings.RootFolder;
            statusLabel.Text = string.Format("Показано {0} з {1}   •   {2}", shown, allEntries.Count, where);
        }

        int BuildFlatList(string filter)
        {
            listView.BeginUpdate();
            listView.Groups.Clear();
            listView.Items.Clear();

            var src = RdpPins.PinnedFirst(settings, RdpSearch.Filter(settings, allEntries, filter));

            var groups = new Dictionary<string, ListViewGroup>();
            ListViewGroup pinnedGroup = null;
            int shown = 0;
            foreach (var entry in src)
            {
                ListViewGroup g;
                if (RdpPins.IsPinned(settings, entry))
                {
                    if (pinnedGroup == null)
                    {
                        pinnedGroup = new ListViewGroup("\0pinned", RdpPins.GroupTitle);
                        listView.Groups.Add(pinnedGroup);
                    }
                    g = pinnedGroup;
                }
                else if (!groups.TryGetValue(entry.RelativeFolder, out g))
                {
                    g = new ListViewGroup(entry.RelativeFolder, entry.RelativeFolder);
                    groups[entry.RelativeFolder] = g;
                    listView.Groups.Add(g);
                }
                var pwdInfo = PasswordManager.Resolve(settings, entry);
                var item = new ListViewItem(g == pinnedGroup && entry.FolderKey.Length > 0
                    ? entry.DisplayName + "   (" + entry.FolderKey + ")"
                    : entry.DisplayName);
                item.SubItems.Add(pwdInfo.Marker);
                item.Tag = entry;
                item.Group = g;
                item.ToolTipText = entry.FullPath + "\nПароль: " + pwdInfo.Description;
                var themeColors = ThemeManager.Get(settings.Theme);
                item.BackColor = (shown % 2 == 1) ? themeColors.ZebraStripe : themeColors.ControlBack;
                item.ForeColor = themeColors.ControlFore;
                listView.Items.Add(item);
                shown++;
            }

            listView.EndUpdate();
            return shown;
        }

        // Будує дерево тек/підключень: на кожному рівні спершу йдуть підпапки
        // (за абеткою), потім файли цього рівня (за абеткою) -- "папки зверху".
        int BuildHierarchyTree()
        {
            RdpTreeBuilder.Populate(treeView, allEntries,
                entry => PasswordMenu.EntryLabel(settings, entry, entry.DisplayName),
                (key, name) => PasswordMenu.FolderLabel(settings, key, name),
                RdpPins.Pinned(settings, allEntries));
            return allEntries.Count;
        }

        List<RdpEntry> SelectedEntries()
        {
            var list = new List<RdpEntry>();
            if (treeView.Visible)
            {
                var entry = treeView.SelectedNode != null ? treeView.SelectedNode.Tag as RdpEntry : null;
                if (entry != null) list.Add(entry);
            }
            else
            {
                foreach (ListViewItem it in listView.SelectedItems)
                {
                    var e = it.Tag as RdpEntry;
                    if (e != null) list.Add(e);
                }
            }
            return list;
        }

        void ConnectSelected()
        {
            var targets = SelectedEntries();
            if (targets.Count == 0) return;
            Launcher.Connect(this, settings, targets);
        }

        void ListView_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            var hit = listView.GetItemAt(e.X, e.Y);
            if (hit == null) return;
            listView.SelectedItems.Clear();
            hit.Selected = true;
            ConnectSelected();
        }

        void ListView_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            var hit = listView.GetItemAt(e.X, e.Y);
            if (hit != null && !hit.Selected)
            {
                listView.SelectedItems.Clear();
                hit.Selected = true;
            }
            if (hit == null)
            {
                // ПКМ на порожньому місці -- лише "Нове підключення" (у кореневій папці).
                listView.SelectedItems.Clear();
                menuOnEmptySpace = true;
            }
            itemMenu.Show(listView, e.Location);
        }

        void ListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                ConnectSelected();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                DeleteSelected();
                e.Handled = true;
            }
        }

        void DeleteSelected()
        {
            var targets = SelectedEntries();
            if (targets.Count > 0 && ConnectionFileOps.Delete(this, settings, targets)) OnFilesChanged(null);
        }

        // Після створення/копіювання/переміщення/видалення файлу: пересканувати папку й, якщо
        // відомо, виділити новий файл.
        void OnFilesChanged(string selectRelativePath)
        {
            RescanAndFill();
            if (selectRelativePath == null) return;

            if (treeView.Visible)
            {
                RdpTreeBuilder.SelectEntry(treeView, selectRelativePath);
                treeView.Focus();
                return;
            }
            foreach (ListViewItem it in listView.Items)
            {
                var entry = it.Tag as RdpEntry;
                if (entry == null || !string.Equals(entry.RelativePath, selectRelativePath, StringComparison.OrdinalIgnoreCase)) continue;
                listView.SelectedItems.Clear();
                it.Selected = true;
                it.Focused = true;
                it.EnsureVisible();
                listView.Focus();
                break;
            }
        }

        void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                RemovePlaceholder();
                txtSearch.Text = "";
                ApplyPlaceholderIfEmpty();
                ApplyFilter();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                if (listView.Visible && listView.Items.Count > 0)
                {
                    listView.SelectedItems.Clear();
                    listView.Items[0].Selected = true;
                    ConnectSelected();
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Down)
            {
                if (listView.Visible && listView.Items.Count > 0)
                {
                    listView.Focus();
                    listView.Items[0].Selected = true;
                    listView.Items[0].Focused = true;
                }
                else if (treeView.Visible && treeView.Nodes.Count > 0)
                {
                    treeView.Focus();
                    treeView.SelectedNode = treeView.Nodes[0];
                }
                e.Handled = true;
            }
        }

        void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5)
            {
                RescanAndFill();
                e.Handled = true;
            }
        }

        void ItemMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            itemMenu.Items.Clear();
            var noEntries = new List<RdpEntry>();

            if (menuOnEmptySpace)
            {
                menuOnEmptySpace = false;
                ConnectionFileOps.AddItems(itemMenu.Items, this, settings, "", noEntries, a => a(), OnFilesChanged);
                return;
            }

            // ПКМ на папці в дереві -- нове підключення в ній, пароль папки, участь у пошуку.
            var folder = treeView.Visible && treeView.SelectedNode != null ? treeView.SelectedNode.Tag as RdpFolder : null;
            if (folder != null)
            {
                ConnectionFileOps.AddItems(itemMenu.Items, this, settings, folder.RelativePath, noEntries, a => a(), OnFilesChanged);
                itemMenu.Items.Add(new ToolStripSeparator());
                PasswordMenu.AddFolderItems(itemMenu.Items, this, settings, folder.RelativePath, false, a => a(), ApplyFilter);
                PasswordMenu.AddFolderSearchItem(itemMenu.Items, settings, folder.RelativePath, ApplyFilter);
                return;
            }

            var targets = SelectedEntries();
            if (targets.Count == 0)
            {
                e.Cancel = true;
                return;
            }

            var miConnect = new ToolStripMenuItem("Підключитися");
            miConnect.Font = new Font(itemMenu.Font, FontStyle.Bold);
            miConnect.Click += (s, ev) => Launcher.Connect(this, settings, targets);
            itemMenu.Items.Add(miConnect);
            RdpPins.AddMenuItem(itemMenu.Items, settings, targets, ApplyFilter);

            itemMenu.Items.Add(new ToolStripSeparator());

            if (targets.Count == 1)
            {
                var miConn = new ToolStripMenuItem("Комп'ютер / користувач...");
                miConn.Click += (s, ev) =>
                {
                    ConnectionEditor.Edit(this, targets[0], settings.Theme);
                    RescanAndFill();
                };
                itemMenu.Items.Add(miConn);
                itemMenu.Items.Add(new ToolStripSeparator());
            }

            ConnectionFileOps.AddItems(itemMenu.Items, this, settings, targets[0].FolderKey, targets, a => a(), OnFilesChanged);

            if (targets.Count == 1)
            {
                var t = targets[0];

                itemMenu.Items.Add(new ToolStripSeparator());

                PasswordMenu.AddEntryItems(itemMenu.Items, this, settings, t, a => a(), ApplyFilter);

                itemMenu.Items.Add(new ToolStripSeparator());

                var miExplorer = new ToolStripMenuItem("Показати у провіднику");
                miExplorer.Click += (s, ev) => Process.Start("explorer.exe", "/select,\"" + t.FullPath + "\"");
                itemMenu.Items.Add(miExplorer);

                var miCopy = new ToolStripMenuItem("Копіювати шлях");
                miCopy.Click += (s, ev) => { try { Clipboard.SetText(t.FullPath); } catch { } };
                itemMenu.Items.Add(miCopy);

                var miEdit = new ToolStripMenuItem("Редагувати файл (Блокнот)");
                miEdit.Click += (s, ev) =>
                {
                    try { Process.Start(new ProcessStartInfo("notepad.exe", "\"" + t.FullPath + "\"") { UseShellExecute = true }); }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                };
                itemMenu.Items.Add(miEdit);
            }
        }

        void OpenSettings()
        {
            SettingsManager.ReloadShared(settings, true);
            using (var f = new SettingsForm(settings))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;

                bool hotkeyChanged = settings.HotkeyModifiers != f.ResultSettings.HotkeyModifiers ||
                                      settings.HotkeyKey != f.ResultSettings.HotkeyKey;
                var oldSharedPath = SettingsManager.SharedPath(settings);
                var oldFolder = settings.SettingsFolder;
                settings = f.ResultSettings;

                bool saveShared = true;
                bool forceOverwrite = false;
                var newSharedPath = SettingsManager.SharedPath(settings);
                if (!string.Equals(oldSharedPath, newSharedPath, StringComparison.OrdinalIgnoreCase) && File.Exists(newSharedPath))
                {
                    var r = MessageBox.Show(this,
                        "У вибраній папці вже є файл налаштувань (наприклад, з іншого ПК):\n" + newSharedPath + "\n\n" +
                        "Так -- завантажити його (паролі й налаштування на цьому ПК буде замінено ним).\n" +
                        "Ні -- перезаписати його поточними налаштуваннями цього ПК.\n" +
                        "Скасувати -- лишити попередню папку.",
                        "Папка налаштувань", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    if (r == DialogResult.No)
                    {
                        forceOverwrite = true;
                    }
                    else if (r == DialogResult.Cancel)
                    {
                        settings.SettingsFolder = oldFolder;
                    }
                    else if (r == DialogResult.Yes)
                    {
                        if (SettingsManager.ReloadShared(settings, false))
                        {
                            saveShared = false;
                            hotkeyChanged = true;
                        }
                        else
                        {
                            MessageBox.Show(this, "Не вдалося прочитати цей файл -- лишаємо попередню папку.",
                                "Папка налаштувань", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            settings.SettingsFolder = oldFolder;
                        }
                    }
                }

                SettingsManager.SaveLocal(settings);
                if (saveShared && !SettingsManager.SaveShared(settings, forceOverwrite))
                {
                    MessageBox.Show(this, "Не вдалося записати файл налаштувань:\n" + SettingsManager.SharedPath(settings),
                        "Налаштування", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                RescanAndFill();
                ApplyTheme();

                if (hotkeyChanged)
                {
                    UnregisterGlobalHotkey();
                    RegisterGlobalHotkey();
                }
            }
        }

        void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!reallyExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                MinimizeToTray();
                return;
            }

            if (WindowState == FormWindowState.Normal)
            {
                settings.WindowWidth = Width;
                settings.WindowHeight = Height;
            }
            SettingsManager.SaveLocal(settings);
            trayIcon.Visible = false;
            UnregisterGlobalHotkey();
        }

        // Повідомлення від другого запущеного екземпляра (див. Program.Main): замість
        // другої копії застосунку -- просто показуємо вже наявне вікно.
        // WM_HOTKEY -- спрацювала глобальна гаряча клавіша.
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Program.WM_SHOWME)
            {
                ShowMainWindow();
            }
            else if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                OpenTraySearch();
            }
            base.WndProc(ref m);
        }
    }

    // Легке спливаюче вікно швидкого пошуку, яке відкривається з трею (правий
    // клік по іконці). На відміну від ToolStripTextBox у ContextMenuStrip трею
    // (нестабільно, спричиняло збої), це звичайна форма -- надійно.
    public class TraySearchForm : Form
    {
        List<RdpEntry> allEntries;
        AppSettings settings;
        IWin32Window connectOwner;
        Action exitAction;

        TextBox txtSearch;
        Button btnViewToggle;
        ListBox lstResults;
        TreeView treeResults;
        bool treeMode;
        ContextMenuStrip resultMenu;
        // Поки відкрито контекстне меню або діалог редагування, вікно втрачає фокус --
        // це не привід закривати його (інакше воно зникне разом із діалогом).
        bool suppressClose;

        // Паролі змінено з цього вікна -- головне вікно оновить свій список після закриття.
        public bool PasswordsChanged { get; private set; }

        const int EdgeMargin = 8;

        // Пересканувати папку з .rdp (у головному вікні) і повернути новий список --
        // після створення/копіювання/переміщення/видалення підключення.
        Func<List<RdpEntry>> rescan;

        public TraySearchForm(List<RdpEntry> allEntries, AppSettings settings, IWin32Window connectOwner, Action exitAction,
            Func<List<RdpEntry>> rescan)
        {
            this.allEntries = allEntries;
            this.rescan = rescan;
            this.settings = settings;
            this.connectOwner = connectOwner;
            this.exitAction = exitAction;
            treeMode = settings.HierarchyView;

            AutoScaleMode = AutoScaleMode.None;
            // Sizable (не Fixed) -- щоб користувач і сам міг розтягнути/звузити вікно,
            // хоча за замовчуванням воно вже відкривається майже на всю висоту екрана.
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            ShowInTaskbar = false;
            TopMost = true;
            Text = "Пошук підключення";
            Font = new Font("Segoe UI", 9.5f);
            MinimumSize = new Size(320, 240);
            KeyPreview = true;

            var screen = Screen.FromPoint(Cursor.Position).WorkingArea;
            Width = 420;
            Height = Math.Max(MinimumSize.Height, screen.Height - 20);

            BuildUi();

            // Невеликий "пільговий" період після появи вікна: коли воно відкривається
            // по глобальній гарячій клавіші, перше отримання/втрата фокуса іноді
            // відбувається з невеликою затримкою (боротьба за фокус з попереднім
            // активним вікном) -- без цієї паузи Deactivate міг спрацювати одразу й
            // закрити вікно, перш ніж користувач встигав його побачити.
            DateTime shownAt = DateTime.MinValue;
            Deactivate += (s, e) =>
            {
                if ((DateTime.UtcNow - shownAt).TotalMilliseconds < 300) return;
                if (suppressClose) return;
                Close();
            };
            Load += (s, e) => { shownAt = DateTime.UtcNow; txtSearch.Focus(); RefreshResults(); };
        }

        void BuildUi()
        {
            const int toggleWidth = 100;
            const int searchHeight = 23;
            const int btnHeight = 26;
            const int btnBottomMargin = 8;
            int resultsTop = EdgeMargin + searchHeight + 6;
            int resultsBottomReserved = btnHeight + btnBottomMargin + 6;

            txtSearch = new TextBox
            {
                Left = EdgeMargin,
                Top = EdgeMargin,
                Width = ClientSize.Width - EdgeMargin * 2 - toggleWidth - 6,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            txtSearch.TextChanged += (s, e) => RefreshResults();
            txtSearch.KeyDown += TxtSearch_KeyDown;

            btnViewToggle = new Button
            {
                Left = ClientSize.Width - EdgeMargin - toggleWidth,
                Top = EdgeMargin - 2,
                Width = toggleWidth,
                Height = searchHeight + 4,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            UpdateToggleButtonText();
            btnViewToggle.Click += (s, e) =>
            {
                treeMode = !treeMode;
                settings.HierarchyView = treeMode;
                SettingsManager.SaveShared(settings);
                UpdateToggleButtonText();
                RefreshResults();
            };

            lstResults = new ListBox
            {
                Left = EdgeMargin,
                Top = resultsTop,
                Width = ClientSize.Width - EdgeMargin * 2,
                Height = ClientSize.Height - resultsTop - resultsBottomReserved,
                IntegralHeight = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            lstResults.KeyDown += LstResults_KeyDown;
            lstResults.FormattingEnabled = true;
            lstResults.Format += (s, e) =>
            {
                var entry = e.ListItem as RdpEntry;
                if (entry != null)
                    e.Value = (RdpPins.IsPinned(settings, entry) ? "📌 " : "") + PasswordMenu.EntryLabel(settings, entry, entry.ToString());
            };
            lstResults.MouseDoubleClick += (s, e) => ConnectSelected();

            treeResults = new TreeView
            {
                Left = EdgeMargin,
                Top = resultsTop,
                Width = ClientSize.Width - EdgeMargin * 2,
                Height = ClientSize.Height - resultsTop - resultsBottomReserved,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ImageList = ShellIcons.GetFolderAndRdpImageList(),
                ShowNodeToolTips = true,
                Visible = false
            };
            treeResults.NodeMouseDoubleClick += (s, e) =>
            {
                treeResults.SelectedNode = e.Node;
                if (e.Node.Tag is RdpEntry) ConnectSelected();
            };
            treeResults.KeyDown += TreeResults_KeyDown;

            resultMenu = new ContextMenuStrip();
            resultMenu.Opening += ResultMenu_Opening;
            resultMenu.Closed += ResultMenu_Closed;
            lstResults.ContextMenuStrip = resultMenu;
            treeResults.ContextMenuStrip = resultMenu;
            // Правий клік має спершу виділити елемент під курсором -- меню з'являється
            // на відпусканні кнопки, тож до цього моменту виділення вже змінене.
            lstResults.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                int i = lstResults.IndexFromPoint(e.Location);
                lstResults.SelectedIndex = i;
            };
            treeResults.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                treeResults.SelectedNode = treeResults.GetNodeAt(e.Location);
            };

            var btnExit = new Button
            {
                Text = "Завершити програму",
                Left = EdgeMargin,
                Top = ClientSize.Height - btnHeight - btnBottomMargin,
                Width = 150,
                Height = btnHeight,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnExit.Click += (s, e) =>
            {
                Close();
                if (this.exitAction != null) this.exitAction();
            };

            Controls.Add(txtSearch);
            Controls.Add(btnViewToggle);
            Controls.Add(lstResults);
            Controls.Add(treeResults);
            Controls.Add(btnExit);

            ThemeManager.Apply(this, settings.Theme);
        }

        // Windows 11 Mica -- вузька смужка навколо результатів пошуку (EdgeMargin)
        // показує розмитий матеріал, якщо ефект увімкнено в Налаштуваннях.
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (settings.MicaEnabled && DwmMica.TryEnable(Handle, settings.Theme == ThemeManager.DarkThemeName))
            {
                BackColor = Color.Black;
            }
        }

        void UpdateToggleButtonText()
        {
            btnViewToggle.Text = treeMode ? "☰ Список" : "🌲 Дерево";
        }

        // Дерево (папки зверху, іконки як у провіднику) показується лише коли пошук
        // порожній і увімкнено відповідний режим; під час активного пошуку -- завжди
        // плаский список збігів, як і раніше.
        void RefreshResults()
        {
            var filter = txtSearch.Text.Trim();
            bool useTree = filter.Length == 0 && treeMode;

            lstResults.Visible = !useTree;
            treeResults.Visible = useTree;

            if (useTree)
            {
                RdpTreeBuilder.Populate(treeResults, allEntries,
                    entry => PasswordMenu.EntryLabel(settings, entry, entry.DisplayName),
                    (key, name) => PasswordMenu.FolderLabel(settings, key, name),
                    RdpPins.Pinned(settings, allEntries));
                if (treeResults.SelectedNode == null && treeResults.Nodes.Count > 0) treeResults.SelectedNode = treeResults.Nodes[0];
                return;
            }

            var matches = RdpSearch.Filter(settings, allEntries, filter);

            lstResults.BeginUpdate();
            lstResults.Items.Clear();
            foreach (var entry in RdpPins.PinnedFirst(settings, matches
                         .OrderBy(e => e.RelativeFolder, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)))
            {
                lstResults.Items.Add(entry);
            }
            lstResults.EndUpdate();

            if (lstResults.Items.Count > 0) lstResults.SelectedIndex = 0;
        }

        void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                ConnectSelected();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Down)
            {
                if (lstResults.Visible && lstResults.Items.Count > 0)
                {
                    lstResults.Focus();
                    lstResults.SelectedIndex = 0;
                    e.Handled = true;
                }
                else if (treeResults.Visible && treeResults.Nodes.Count > 0)
                {
                    treeResults.Focus();
                    if (treeResults.SelectedNode == null) treeResults.SelectedNode = treeResults.Nodes[0];
                    e.Handled = true;
                }
            }
        }

        void LstResults_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                ConnectSelected();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        void TreeResults_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                var node = treeResults.SelectedNode;
                if (node == null) return;
                if (node.Tag is RdpEntry) ConnectSelected();
                else node.Toggle();
                e.Handled = true;
            }
        }

        // Після активації вікна з трея / по гарячій клавіші -- одразу друкувати в пошук.
        public void FocusSearchBox()
        {
            txtSearch.Focus();
            txtSearch.SelectionStart = txtSearch.TextLength;
        }

        RdpEntry SelectedEntry()
        {
            return treeResults.Visible
                ? (treeResults.SelectedNode != null ? treeResults.SelectedNode.Tag as RdpEntry : null)
                : lstResults.SelectedItem as RdpEntry;
        }

        void ConnectSelected()
        {
            var entry = SelectedEntry();
            if (entry == null) return;

            Launcher.Connect(connectOwner, settings, new[] { entry });
            Close();
        }

        void ResultMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var entry = SelectedEntry();
            resultMenu.Items.Clear();

            // ПКМ на папці в дереві -- лише керування паролем папки.
            var folder = treeResults.Visible && treeResults.SelectedNode != null ? treeResults.SelectedNode.Tag as RdpFolder : null;
            var noEntries = new List<RdpEntry>();
            if (treeResults.Visible && treeResults.SelectedNode != null && treeResults.SelectedNode.Tag == RdpPins.GroupTag)
            {
                e.Cancel = true;
                return;
            }
            if (folder == null && entry == null)
            {
                // ПКМ на порожньому місці -- лише "Нове підключення" (у кореневій папці).
                suppressClose = true;
                ConnectionFileOps.AddItems(resultMenu.Items, this, settings, "", noEntries, RunModal, OnFilesChanged);
                return;
            }
            if (folder != null)
            {
                suppressClose = true;
                ConnectionFileOps.AddItems(resultMenu.Items, this, settings, folder.RelativePath, noEntries, RunModal, OnFilesChanged);
                resultMenu.Items.Add(new ToolStripSeparator());
                PasswordMenu.AddFolderItems(resultMenu.Items, this, settings, folder.RelativePath, false, RunModal, OnPasswordsChanged);
                PasswordMenu.AddFolderSearchItem(resultMenu.Items, settings, folder.RelativePath, OnPasswordsChanged);
                return;
            }

            suppressClose = true;

            var miConnect = new ToolStripMenuItem("Підключитися");
            miConnect.Font = new Font(resultMenu.Font, FontStyle.Bold);
            miConnect.Click += (s, ev) => ConnectSelected();
            resultMenu.Items.Add(miConnect);
            RdpPins.AddMenuItem(resultMenu.Items, settings, new List<RdpEntry> { entry }, OnPinsChanged);

            var miEdit = new ToolStripMenuItem("Комп'ютер / користувач...");
            miEdit.Click += (s, ev) =>
            {
                suppressClose = true;
                try { ConnectionEditor.Edit(this, entry, settings.Theme); }
                finally { suppressClose = false; }
            };
            resultMenu.Items.Add(miEdit);

            resultMenu.Items.Add(new ToolStripSeparator());
            ConnectionFileOps.AddItems(resultMenu.Items, this, settings, entry.FolderKey, new List<RdpEntry> { entry }, RunModal, OnFilesChanged);

            resultMenu.Items.Add(new ToolStripSeparator());
            PasswordMenu.AddEntryItems(resultMenu.Items, this, settings, entry, RunModal, OnPasswordsChanged);
        }

        // Після дії з файлом: головне вікно пересканує папку, а тут оновлюємо результати
        // (дерево зберігає розгорнуті папки) і виділяємо новий файл, якщо він є.
        void OnFilesChanged(string selectRelativePath)
        {
            PasswordsChanged = true;
            if (rescan != null) allEntries = rescan();
            RefreshResults();
            if (selectRelativePath == null) return;

            if (treeResults.Visible)
            {
                RdpTreeBuilder.SelectEntry(treeResults, selectRelativePath);
                return;
            }
            for (int i = 0; i < lstResults.Items.Count; i++)
            {
                var item = lstResults.Items[i] as RdpEntry;
                if (item != null && string.Equals(item.RelativePath, selectRelativePath, StringComparison.OrdinalIgnoreCase))
                {
                    lstResults.SelectedIndex = i;
                    break;
                }
            }
        }

        // Закріплення змінилось -- перебудувати результати (група "Закріплені", порядок).
        void OnPinsChanged()
        {
            PasswordsChanged = true;
            RefreshResults();
        }

        // Діалог із меню забирає фокус -- на цей час вимикаємо автозакриття вікна.
        void RunModal(Action action)
        {
            suppressClose = true;
            try { action(); }
            finally { suppressClose = false; }
        }

        // Оновлюємо лише підписи (мітки паролів), не перебудовуючи дерево -- щоб не
        // згортались уже розгорнуті папки.
        void OnPasswordsChanged()
        {
            PasswordsChanged = true;

            // ListBox запам'ятовує текст рядка при додаванні (Format більше не викликається),
            // тож перезаповнюємо ті самі елементи, зберігаючи виділення й прокрутку.
            int selected = lstResults.SelectedIndex, top = lstResults.TopIndex;
            var items = lstResults.Items.Cast<object>().ToArray();
            lstResults.BeginUpdate();
            lstResults.Items.Clear();
            lstResults.Items.AddRange(items);
            if (selected < lstResults.Items.Count) lstResults.SelectedIndex = selected;
            if (top < lstResults.Items.Count) lstResults.TopIndex = top;
            lstResults.EndUpdate();

            UpdateNodeLabels(treeResults.Nodes);
        }

        void UpdateNodeLabels(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                var entry = node.Tag as RdpEntry;
                var folder = node.Tag as RdpFolder;
                if (entry != null)
                {
                    node.Text = PasswordMenu.EntryLabel(settings, entry, entry.DisplayName);
                    if (node.Parent != null && node.Parent.Tag == RdpPins.GroupTag && entry.FolderKey.Length > 0)
                        node.Text += "   (" + entry.FolderKey + ")";
                }
                else if (folder != null)
                {
                    var name = folder.RelativePath.Substring(folder.RelativePath.LastIndexOf('\\') + 1);
                    node.Text = PasswordMenu.FolderLabel(settings, folder.RelativePath, name);
                }
                UpdateNodeLabels(node.Nodes);
            }
        }

        // Закрите меню (вибір пункту, Esc або клік повз нього) знову вмикає звичайну
        // поведінку "закритися при втраті фокуса"; якщо вікно після кліку повз меню
        // вже не активне -- закриваємо його, як зробив би Deactivate.
        void ResultMenu_Closed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            BeginInvoke(new Action(() =>
            {
                suppressClose = false;
                if (!IsDisposed && Form.ActiveForm == null && e.CloseReason != ToolStripDropDownCloseReason.ItemClicked)
                {
                    Close();
                }
            }));
        }
    }

    // Створення, копіювання, переміщення й видалення файлів підключень (ПКМ) -- спільне
    // для головного вікна та вікна швидкого пошуку з трея. Збережений пароль підключення
    // (ключ -- шлях відносно кореневої папки) переїжджає/копіюється/видаляється разом із файлом.
    public static class ConnectionFileOps
    {
        // Типовий вміст нового .rdp -- ті самі ключі, що пише mstsc ("Зберегти як").
        static readonly string[] NewFileTemplate =
        {
            "screen mode id:i:2",
            "use multimon:i:0",
            "session bpp:i:32",
            "compression:i:1",
            "keyboardhook:i:2",
            "audiocapturemode:i:0",
            "videoplaybackmode:i:1",
            "connection type:i:7",
            "networkautodetect:i:1",
            "bandwidthautodetect:i:1",
            "displayconnectionbar:i:1",
            "enableworkspacereconnect:i:0",
            "disable wallpaper:i:0",
            "allow font smoothing:i:1",
            "allow desktop composition:i:1",
            "autoreconnection enabled:i:1",
            "authentication level:i:2",
            "prompt for credentials:i:0",
            "negotiate security layer:i:1",
            "audiomode:i:0",
            "redirectclipboard:i:1",
            "redirectprinters:i:0",
            "redirectsmartcards:i:0",
            "drivestoredirect:s:"
        };

        static string FolderFullPath(AppSettings s, string folderKey)
        {
            return string.IsNullOrEmpty(folderKey) ? s.RootFolder : Path.Combine(s.RootFolder, folderKey);
        }

        static string RelativeOf(string folderKey, string fileName)
        {
            return string.IsNullOrEmpty(folderKey) ? fileName : folderKey + "\\" + fileName;
        }

        static bool RootReady(IWin32Window owner, AppSettings s)
        {
            if (!string.IsNullOrWhiteSpace(s.RootFolder) && Directory.Exists(s.RootFolder)) return true;
            MessageBox.Show(owner, "Спершу вкажіть у Налаштуваннях папку з файлами *.rdp.", "Підключення",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        // Назва файлу без розширення: непорожня, без заборонених символів Windows.
        public static string ValidateName(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) return "Вкажіть назву підключення.";
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return "Назва не може містити символи  \\ / : * ? \" < > |";
            if (name.EndsWith(".")) return "Назва не може закінчуватися крапкою.";
            return null;
        }

        // Повертає шлях нового файлу відносно кореня або null, якщо скасовано.
        public static string CreateNew(IWin32Window owner, AppSettings s, string folderKey)
        {
            if (!RootReady(owner, s)) return null;
            folderKey = folderKey ?? "";
            var where = folderKey.Length == 0 ? "(коренева папка)" : folderKey;

            using (var dlg = new NewConnectionDialog(where, s.Theme))
            {
                while (true)
                {
                    if (dlg.ShowDialog(owner) != DialogResult.OK) return null;

                    if (File.Exists(Path.Combine(FolderFullPath(s, folderKey), dlg.ConnectionName + ".rdp")))
                    {
                        MessageBox.Show(owner, "Підключення з такою назвою в цій папці вже є.", "Нове підключення",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        continue;
                    }
                    try
                    {
                        return CreateFile(s, folderKey, dlg.ConnectionName, dlg.Address, dlg.User, dlg.Password);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(owner, "Не вдалося створити файл:\n" + ex.Message, "Нове підключення",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return null;
                    }
                }
            }
        }

        // Копія в тій самій папці під новою назвою (разом із власним паролем підключення).
        public static string Duplicate(IWin32Window owner, AppSettings s, RdpEntry e)
        {
            if (!RootReady(owner, s)) return null;
            var folder = Path.GetDirectoryName(e.FullPath);
            var suggested = UniqueName(folder, e.DisplayName + " - копія");

            using (var dlg = new TextPromptDialog("Копіювати підключення", "Назва копії \"" + e.DisplayName + "\":", suggested, s.Theme))
            {
                while (true)
                {
                    if (dlg.ShowDialog(owner) != DialogResult.OK) return null;
                    var error = ValidateName(dlg.Value);
                    if (error != null)
                    {
                        MessageBox.Show(owner, error, "Копіювати підключення", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        continue;
                    }

                    var fileName = dlg.Value.Trim() + ".rdp";
                    var target = Path.Combine(folder, fileName);
                    if (File.Exists(target))
                    {
                        MessageBox.Show(owner, "Підключення з такою назвою в цій папці вже є.", "Копіювати підключення",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        continue;
                    }

                    try { return CopyFile(s, e, dlg.Value.Trim()); }
                    catch (Exception ex)
                    {
                        MessageBox.Show(owner, "Не вдалося скопіювати файл:\n" + ex.Message, "Копіювати підключення",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return null;
                    }
                }
            }
        }

        // Переміщення одного чи кількох файлів у вибрану папку. true -- щось переміщено.
        public static bool Move(IWin32Window owner, AppSettings s, IList<RdpEntry> entries)
        {
            if (entries.Count == 0 || !RootReady(owner, s)) return false;

            string targetKey;
            using (var dlg = new FolderPickerDialog(s.RootFolder, entries[0].FolderKey, s.Theme,
                entries.Count == 1 ? "Перемістити \"" + entries[0].DisplayName + "\"" : "Перемістити підключення (" + entries.Count + ")"))
            {
                if (dlg.ShowDialog(owner) != DialogResult.OK) return false;
                targetKey = dlg.SelectedFolderKey;
            }

            var problems = new List<string>();
            int moved = MoveFiles(s, entries, targetKey, problems);
            if (problems.Count > 0)
            {
                MessageBox.Show(owner, "Не вдалося перемістити:\n\n" + string.Join("\n", problems), "Перемістити підключення",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return moved > 0;
        }

        // Видалення в Кошик (не остаточно) разом із власним паролем підключення.
        public static bool Delete(IWin32Window owner, AppSettings s, IList<RdpEntry> entries)
        {
            if (entries.Count == 0) return false;
            var question = entries.Count == 1
                ? "Видалити підключення \"" + entries[0].DisplayName + "\"?"
                : "Видалити підключення (" + entries.Count + "):\n\n" + string.Join("\n", entries.Take(15).Select(x => x.ToString())) +
                  (entries.Count > 15 ? "\n..." : "") + "\n\n";
            var r = MessageBox.Show(owner, question + "\nФайл буде переміщено в Кошик, збережений пароль підключення -- видалено.",
                "Видалити підключення", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (r != DialogResult.Yes) return false;

            var problems = new List<string>();
            int deleted = DeleteFiles(s, entries, problems);
            if (problems.Count > 0)
            {
                MessageBox.Show(owner, "Не вдалося видалити:\n\n" + string.Join("\n", problems), "Видалити підключення",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return deleted > 0;
        }

        // ---- Самі операції з файлами (без діалогів) ----

        public static string CreateFile(AppSettings s, string folderKey, string name, string address, string user, string password)
        {
            var fileName = name + ".rdp";
            var lines = new List<string>(NewFileTemplate) { "full address:s:" + address };
            if (!string.IsNullOrEmpty(user)) lines.Add("username:s:" + user);
            // FileMode.CreateNew -- ніколи не перезаписуємо наявний файл.
            using (var fs = new FileStream(Path.Combine(FolderFullPath(s, folderKey), fileName), FileMode.CreateNew))
            using (var w = new StreamWriter(fs, new UnicodeEncoding(false, true)))
            {
                // UTF-16 з BOM -- так зберігає .rdp сам mstsc.
                w.Write(string.Join("\r\n", lines) + "\r\n");
            }

            var rel = RelativeOf(folderKey, fileName);
            if (!string.IsNullOrEmpty(password))
            {
                PasswordMenu.ChangeShared(s, () => s.FilePasswords[rel] =
                    new SavedCredential { Host = address, User = user ?? "", Password = password });
            }
            return rel;
        }

        public static string CopyFile(AppSettings s, RdpEntry e, string newName)
        {
            var fileName = newName + ".rdp";
            File.Copy(e.FullPath, Path.Combine(Path.GetDirectoryName(e.FullPath), fileName));

            var rel = RelativeOf(e.FolderKey, fileName);
            SavedCredential own;
            if (s.FilePasswords.TryGetValue(e.RelativePath, out own))
            {
                PasswordMenu.ChangeShared(s, () => s.FilePasswords[rel] =
                    new SavedCredential { Host = own.Host, User = own.User, Password = own.Password });
            }
            return rel;
        }

        public static int MoveFiles(AppSettings s, IList<RdpEntry> entries, string targetKey, List<string> problems)
        {
            var moved = new List<KeyValuePair<string, string>>();
            foreach (var e in entries)
            {
                if (string.Equals(e.FolderKey, targetKey, StringComparison.OrdinalIgnoreCase)) continue;
                var fileName = Path.GetFileName(e.FullPath);
                var target = Path.Combine(FolderFullPath(s, targetKey), fileName);
                if (File.Exists(target))
                {
                    problems.Add(e.DisplayName + " -- у цій папці вже є файл із такою назвою");
                    continue;
                }
                try
                {
                    File.Move(e.FullPath, target);
                    moved.Add(new KeyValuePair<string, string>(e.RelativePath, RelativeOf(targetKey, fileName)));
                }
                catch (Exception ex)
                {
                    problems.Add(e.DisplayName + " -- " + ex.Message);
                }
            }

            if (moved.Count > 0)
            {
                PasswordMenu.ChangeShared(s, () =>
                {
                    foreach (var m in moved)
                    {
                        RdpPins.Rename(s, m.Key, m.Value);
                        SavedCredential own;
                        if (!s.FilePasswords.TryGetValue(m.Key, out own)) continue;
                        s.FilePasswords.Remove(m.Key);
                        s.FilePasswords[m.Value] = own;
                    }
                });
            }
            return moved.Count;
        }

        public static int DeleteFiles(AppSettings s, IList<RdpEntry> entries, List<string> problems)
        {
            var deleted = new List<string>();
            foreach (var e in entries)
            {
                try
                {
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(e.FullPath,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                    deleted.Add(e.RelativePath);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    problems.Add(e.DisplayName + " -- " + ex.Message);
                }
            }

            if (deleted.Count > 0)
            {
                PasswordMenu.ChangeShared(s, () =>
                {
                    foreach (var rel in deleted)
                    {
                        s.FilePasswords.Remove(rel);
                        RdpPins.Remove(s, rel);
                    }
                });
            }
            return deleted.Count;
        }

        static string UniqueName(string folder, string baseName)
        {
            var name = baseName;
            for (int i = 2; File.Exists(Path.Combine(folder, name + ".rdp")); i++) name = baseName + " (" + i + ")";
            return name;
        }

        // Пункти ПКМ для файлів/папки. contextFolderKey -- папка, де створювати нове
        // підключення; entries -- виділені підключення (може бути порожньо).
        // onChanged(шлях нового/виділеного файлу або null) -- пересканувати й оновити вигляд.
        public static void AddItems(ToolStripItemCollection items, IWin32Window owner, AppSettings s,
            string contextFolderKey, IList<RdpEntry> entries, Action<Action> runModal, Action<string> onChanged)
        {
            var miNew = new ToolStripMenuItem("Нове підключення...");
            miNew.Click += (o, ev) => runModal(() =>
            {
                var rel = CreateNew(owner, s, contextFolderKey);
                if (rel != null) onChanged(rel);
            });
            items.Add(miNew);

            if (entries.Count == 1)
            {
                var e = entries[0];
                var miCopy = new ToolStripMenuItem("Копіювати підключення...");
                miCopy.Click += (o, ev) => runModal(() =>
                {
                    var rel = Duplicate(owner, s, e);
                    if (rel != null) onChanged(rel);
                });
                items.Add(miCopy);
            }

            if (entries.Count > 0)
            {
                var list = entries.ToList();
                var miMove = new ToolStripMenuItem("Перемістити в папку...");
                miMove.Click += (o, ev) => runModal(() => { if (Move(owner, s, list)) onChanged(null); });
                items.Add(miMove);

                var miDelete = new ToolStripMenuItem(list.Count == 1 ? "Видалити підключення" : "Видалити підключення (" + list.Count + ")");
                miDelete.Click += (o, ev) => runModal(() => { if (Delete(owner, s, list)) onChanged(null); });
                items.Add(miDelete);
            }
        }
    }

    // Діалог нового підключення: назва файлу, комп'ютер, користувач і (необов'язково) пароль.
    public class NewConnectionDialog : Form
    {
        public string ConnectionName;
        public string Address;
        public string User;
        public string Password;

        TextBox txtName;
        TextBox txtAddress;
        TextBox txtUser;
        TextBox txtPassword;

        public NewConnectionDialog(string folderDisplay, string themeName)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = "Нове підключення";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Width = 380;
            Height = 372;
            Font = new Font("Segoe UI", 9.5f);

            var lblWhere = new Label { Text = "Папка: " + folderDisplay, Left = 12, Top = 12, Width = 340, ForeColor = SystemColors.GrayText };

            var lblName = new Label { Text = "Назва підключення:", Left = 12, Top = 38, Width = 340 };
            txtName = new TextBox { Left = 12, Top = 60, Width = 340 };

            var lblAddress = new Label { Text = "Комп'ютер (адреса, можна з портом):", Left = 12, Top = 92, Width = 340 };
            txtAddress = new TextBox { Left = 12, Top = 114, Width = 340 };

            var lblUser = new Label { Text = "Користувач:", Left = 12, Top = 146, Width = 340 };
            txtUser = new TextBox { Left = 12, Top = 168, Width = 340 };

            var lblPassword = new Label { Text = "Пароль (необов'язково):", Left = 12, Top = 200, Width = 340 };
            txtPassword = new TextBox { Left = 12, Top = 222, Width = 340, UseSystemPasswordChar = true };
            var chkShow = new CheckBox { Text = "Показати пароль", Left = 12, Top = 250, Width = 200 };
            chkShow.CheckedChanged += (s, e) => txtPassword.UseSystemPasswordChar = !chkShow.Checked;

            // Поки назву не змінювали вручну -- підставляємо адресу комп'ютера.
            bool nameEdited = false;
            txtName.TextChanged += (s, e) => { if (txtName.Focused) nameEdited = true; };
            txtAddress.TextChanged += (s, e) => { if (!nameEdited) txtName.Text = txtAddress.Text.Trim().Replace(':', '_'); };

            var btnOk = new Button { Text = "Створити", Left = 172, Top = 286, Width = 85 };
            btnOk.Click += (s, e) => Accept();
            var btnCancel = new Button { Text = "Скасувати", Left = 264, Top = 286, Width = 85, DialogResult = DialogResult.Cancel };

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.AddRange(new Control[]
            {
                lblWhere, lblName, txtName, lblAddress, txtAddress, lblUser, txtUser,
                lblPassword, txtPassword, chkShow, btnOk, btnCancel
            });

            ThemeManager.Apply(this, themeName);
            lblWhere.ForeColor = SystemColors.GrayText;

            Load += (s, e) => txtAddress.Focus();
        }

        void Accept()
        {
            var address = txtAddress.Text.Trim();
            if (address.Length == 0)
            {
                MessageBox.Show(this, "Вкажіть ім'я або адресу комп'ютера.", "Перевірка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var error = ConnectionFileOps.ValidateName(txtName.Text);
            if (error != null)
            {
                MessageBox.Show(this, error, "Перевірка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ConnectionName = txtName.Text.Trim();
            Address = address;
            User = txtUser.Text.Trim();
            Password = txtPassword.Text;
            DialogResult = DialogResult.OK;
        }
    }

    // Простий діалог з одним текстовим полем (назва копії, нової папки).
    public class TextPromptDialog : Form
    {
        public string Value;
        TextBox txtValue;

        public TextPromptDialog(string title, string label, string initial, string themeName)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Width = 380;
            Height = 160;
            Font = new Font("Segoe UI", 9.5f);

            var lbl = new Label { Text = label, Left = 12, Top = 16, Width = 340 };
            txtValue = new TextBox { Left = 12, Top = 38, Width = 340, Text = initial ?? "" };

            var btnOk = new Button { Text = "OK", Left = 172, Top = 76, Width = 85 };
            btnOk.Click += (s, e) => { Value = txtValue.Text; DialogResult = DialogResult.OK; };
            var btnCancel = new Button { Text = "Скасувати", Left = 264, Top = 76, Width = 85, DialogResult = DialogResult.Cancel };

            AcceptButton = btnOk;
            CancelButton = btnCancel;
            Controls.AddRange(new Control[] { lbl, txtValue, btnOk, btnCancel });

            ThemeManager.Apply(this, themeName);
            Load += (s, e) => { txtValue.Focus(); txtValue.SelectAll(); };
        }
    }

    // Вибір папки (усередині кореневої) для переміщення підключень; можна створити нову.
    public class FolderPickerDialog : Form
    {
        public string SelectedFolderKey;

        readonly string root;
        readonly string themeName;
        TreeView tree;

        public FolderPickerDialog(string root, string currentFolderKey, string themeName, string title)
        {
            this.root = root.TrimEnd('\\', '/');
            this.themeName = themeName;

            AutoScaleMode = AutoScaleMode.None;
            Text = title;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Width = 420;
            Height = 480;
            MinimumSize = new Size(320, 300);
            Font = new Font("Segoe UI", 9.5f);

            var lbl = new Label { Text = "Оберіть папку:", Left = 12, Top = 12, Width = 380 };
            tree = new TreeView
            {
                Left = 12,
                Top = 34,
                Width = ClientSize.Width - 24,
                Height = ClientSize.Height - 34 - 52,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                HideSelection = false,
                ImageList = ShellIcons.GetFolderAndRdpImageList(),
                ImageIndex = ShellIcons.FolderImageIndex,
                SelectedImageIndex = ShellIcons.FolderImageIndex
            };
            tree.NodeMouseDoubleClick += (s, e) => Accept();

            int btnTop = ClientSize.Height - 40;
            var btnNewFolder = new Button { Text = "Нова папка...", Left = 12, Top = btnTop, Width = 110, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            btnNewFolder.Click += (s, e) => CreateFolder();
            var btnOk = new Button { Text = "Перемістити", Left = ClientSize.Width - 12 - 85 - 8 - 100, Top = btnTop, Width = 100, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            btnOk.Click += (s, e) => Accept();
            var btnCancel = new Button { Text = "Скасувати", Left = ClientSize.Width - 12 - 85, Top = btnTop, Width = 85, Anchor = AnchorStyles.Bottom | AnchorStyles.Right, DialogResult = DialogResult.Cancel };

            AcceptButton = btnOk;
            CancelButton = btnCancel;
            Controls.AddRange(new Control[] { lbl, tree, btnNewFolder, btnOk, btnCancel });

            ThemeManager.Apply(this, themeName);
            Fill(currentFolderKey);
        }

        void Fill(string selectKey)
        {
            tree.BeginUpdate();
            tree.Nodes.Clear();
            var rootNode = new TreeNode("(коренева папка)") { Tag = "" };
            tree.Nodes.Add(rootNode);
            AddDirs(rootNode, root);
            rootNode.Expand();
            tree.EndUpdate();

            var node = Find(tree.Nodes, selectKey ?? "") ?? rootNode;
            node.EnsureVisible();
            tree.SelectedNode = node;
        }

        void AddDirs(TreeNode parent, string dir)
        {
            string[] subdirs;
            try { subdirs = Directory.GetDirectories(dir); }
            catch { return; }
            foreach (var sd in subdirs.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var key = sd.Substring(root.Length).TrimStart('\\', '/');
                var node = new TreeNode(Path.GetFileName(sd)) { Tag = key };
                parent.Nodes.Add(node);
                AddDirs(node, sd);
            }
        }

        static TreeNode Find(TreeNodeCollection nodes, string key)
        {
            foreach (TreeNode n in nodes)
            {
                if (string.Equals((string)n.Tag, key, StringComparison.OrdinalIgnoreCase)) return n;
                var found = Find(n.Nodes, key);
                if (found != null) return found;
            }
            return null;
        }

        string CurrentKey
        {
            get { return tree.SelectedNode != null ? (string)tree.SelectedNode.Tag : ""; }
        }

        void CreateFolder()
        {
            var parentKey = CurrentKey;
            using (var dlg = new TextPromptDialog("Нова папка", "Назва нової папки в \"" +
                (parentKey.Length == 0 ? "(коренева папка)" : parentKey) + "\":", "", themeName))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                var name = (dlg.Value ?? "").Trim();
                if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    MessageBox.Show(this, "Некоректна назва папки.", "Нова папка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                var newKey = parentKey.Length == 0 ? name : parentKey + "\\" + name;
                try { Directory.CreateDirectory(Path.Combine(root, newKey)); }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Не вдалося створити папку:\n" + ex.Message, "Нова папка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                Fill(newKey);
            }
        }

        void Accept()
        {
            if (tree.SelectedNode == null) return;
            SelectedFolderKey = CurrentKey;
            DialogResult = DialogResult.OK;
        }
    }

    // Діалог введення пароля. withUser -- додатково поле "Користувач" (облікові дані за
    // замовчуванням у Налаштуваннях; порожній користувач -- для будь-якого).
    public class PasswordDialog : Form
    {
        public string Password;
        public string User;

        TextBox txtUser;
        TextBox txtPassword;
        CheckBox chkShow;

        public PasswordDialog(string title, string themeName, bool withUser = false, string user = "", string password = "")
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Width = 380;
            Font = new Font("Segoe UI", 9.5f);

            int top = 16;
            if (withUser)
            {
                var lblUser = new Label { Text = "Користувач (порожньо -- для будь-якого):", Left = 12, Top = top, Width = 340 };
                txtUser = new TextBox { Left = 12, Top = top + 22, Width = 340, Text = user ?? "" };
                Controls.Add(lblUser);
                Controls.Add(txtUser);
                top += 54;
            }

            var lbl = new Label { Text = "Пароль:", Left = 12, Top = top, Width = 340 };
            txtPassword = new TextBox { Left = 12, Top = top + 22, Width = 340, UseSystemPasswordChar = true, Text = password ?? "" };

            chkShow = new CheckBox { Text = "Показати пароль", Left = 12, Top = top + 50, Width = 200 };
            chkShow.CheckedChanged += (s, e) => txtPassword.UseSystemPasswordChar = !chkShow.Checked;

            var btnOk = new Button { Text = "Зберегти", Left = 172, Top = top + 80, Width = 85 };
            btnOk.Click += (s, e) => Accept();
            var btnCancel = new Button { Text = "Скасувати", Left = 264, Top = top + 80, Width = 85, DialogResult = DialogResult.Cancel };
            Height = top + 154;

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.Add(lbl);
            Controls.Add(txtPassword);
            Controls.Add(chkShow);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            ThemeManager.Apply(this, themeName);

            Load += (s, e) => { if (txtUser != null) txtUser.Focus(); else txtPassword.Focus(); };
        }

        void Accept()
        {
            if (string.IsNullOrEmpty(txtPassword.Text))
            {
                MessageBox.Show(this, "Введіть пароль.", "Перевірка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Password = txtPassword.Text;
            User = txtUser != null ? txtUser.Text.Trim() : null;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    // Діалог перегляду/зміни імені комп'ютера й користувача підключення -- ті самі
    // поля, що в стандартному вікні mstsc ("Комп'ютер" / "Користувач").
    public class ConnectionEditDialog : Form
    {
        public string Address;
        public string User;

        TextBox txtAddress;
        TextBox txtUser;

        public ConnectionEditDialog(string forName, string address, string user, string themeName)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = "Підключення \"" + forName + "\"";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Width = 380;
            Height = 215;
            Font = new Font("Segoe UI", 9.5f);

            var lblAddress = new Label { Text = "Комп'ютер:", Left = 12, Top = 16, Width = 340 };
            txtAddress = new TextBox { Left = 12, Top = 38, Width = 340, Text = address };

            var lblUser = new Label { Text = "Користувач:", Left = 12, Top = 70, Width = 340 };
            txtUser = new TextBox { Left = 12, Top = 92, Width = 340, Text = user };

            var btnOk = new Button { Text = "Зберегти", Left = 172, Top = 128, Width = 85 };
            btnOk.Click += (s, e) => Accept();
            var btnCancel = new Button { Text = "Скасувати", Left = 264, Top = 128, Width = 85, DialogResult = DialogResult.Cancel };

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.Add(lblAddress);
            Controls.Add(txtAddress);
            Controls.Add(lblUser);
            Controls.Add(txtUser);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            ThemeManager.Apply(this, themeName);

            Load += (s, e) => { txtAddress.Focus(); txtAddress.SelectAll(); };
        }

        void Accept()
        {
            var address = txtAddress.Text.Trim();
            if (address.Length == 0)
            {
                MessageBox.Show(this, "Вкажіть ім'я або адресу комп'ютера.", "Перевірка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Address = address;
            User = txtUser.Text.Trim();
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    // Спільна логіка для головного вікна й вікна швидкого пошуку: прочитати значення
    // з файлу, показати діалог і записати зміни назад в оригінальний .rdp.
    public static class ConnectionEditor
    {
        public static void Edit(IWin32Window owner, RdpEntry entry, string themeName)
        {
            string address, user;
            try
            {
                RdpFileEditor.Read(entry.FullPath, out address, out user);
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, "Не вдалося прочитати файл:\n" + ex.Message, "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var dlg = new ConnectionEditDialog(entry.DisplayName, address, user, themeName))
            {
                if (dlg.ShowDialog(owner) != DialogResult.OK) return;
                if (dlg.Address == address && dlg.User == user) return;

                try
                {
                    RdpFileEditor.Save(entry.FullPath, dlg.Address, dlg.User);
                    entry.RefreshConnectionInfo();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(owner, "Не вдалося зберегти файл:\n" + ex.Message, "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
    }

    public class SettingsForm : Form
    {
        AppSettings working;
        public AppSettings ResultSettings;

        const int RightCol = 476;

        TextBox txtRoot;
        ListView lvDefaults;
        TextBox txtHotkey;
        uint capturedHotkeyModifiers;
        uint capturedHotkeyVk;
        RadioButton radioThemeLight;
        RadioButton radioThemeDark;
        RadioButton radioThemeBlue;
        CheckBox chkMica;
        TextBox txtSettingsFolder;
        CheckBox chkStoreInWindows;
        CheckBox chkAutostart;
        Label lblRdpWarningState;
        Button btnRdpWarning;

        public SettingsForm(AppSettings current)
        {
            AutoScaleMode = AutoScaleMode.None;
            working = current.Clone();

            Text = "Налаштування";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Width = 948;
            Height = 572;
            Font = new Font("Segoe UI", 9.5f);

            capturedHotkeyModifiers = working.HotkeyModifiers;
            capturedHotkeyVk = working.HotkeyKey;

            BuildUi();
        }

        Label Hint(int left, int top, int height, string text)
        {
            return new Label { Left = left, Top = top, Width = 440, Height = height, ForeColor = SystemColors.GrayText, Text = text };
        }

        void BuildUi()
        {
            // ---- Ліва колонка: папка з .rdp, облікові дані за замовчуванням, гаряча клавіша, тема ----
            var lblRoot = new Label { Text = "Папка з файлами *.rdp (окремо для кожного ПК):", Left = 12, Top = 14, Width = 440 };
            txtRoot = new TextBox { Left = 12, Top = 36, Width = 340, Text = working.RootFolder };
            var btnBrowse = new Button { Text = "Огляд...", Left = 360, Top = 34, Width = 90 };
            btnBrowse.Click += (s, e) => BrowseInto(txtRoot);

            var lblDefaults = new Label
            {
                Text = "Облікові дані за замовчуванням (якщо немає облікових даних Windows і пароля підключення чи папки):",
                Left = 12,
                Top = 72,
                Width = 440,
                Height = 34
            };
            lvDefaults = new ListView
            {
                Left = 12,
                Top = 108,
                Width = 340,
                Height = 104,
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                MultiSelect = false,
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };
            lvDefaults.Columns.Add("Користувач", 210);
            lvDefaults.Columns.Add("Пароль", 110);
            lvDefaults.DoubleClick += (s, e) => EditDefault();
            FillDefaults();

            var btnAddDefault = new Button { Text = "Додати...", Left = 360, Top = 106, Width = 90 };
            btnAddDefault.Click += (s, e) => AddDefault();
            var btnEditDefault = new Button { Text = "Змінити...", Left = 360, Top = 140, Width = 90 };
            btnEditDefault.Click += (s, e) => EditDefault();
            var btnDelDefault = new Button { Text = "Видалити", Left = 360, Top = 174, Width = 90 };
            btnDelDefault.Click += (s, e) =>
            {
                if (lvDefaults.SelectedIndices.Count == 0) return;
                working.DefaultCredentials.RemoveAt(lvDefaults.SelectedIndices[0]);
                FillDefaults();
            };
            var lblDefaultsHint = Hint(12, 216, 34,
                "Підставляється, коли користувач у .rdp збігається (\"admin\" підходить і до \"SERVER\\admin\"); порожній користувач -- для будь-якого.");

            var lblHotkey = new Label { Text = "Гаряча клавіша (відкриває вікно пошуку з будь-якого місця):", Left = 12, Top = 260, Width = 440 };
            txtHotkey = new TextBox
            {
                Left = 12,
                Top = 282,
                Width = 250,
                ReadOnly = true,
                Text = HotkeyUtil.Format(capturedHotkeyModifiers, capturedHotkeyVk)
            };
            txtHotkey.KeyDown += TxtHotkey_KeyDown;

            var btnResetHotkey = new Button { Text = "Типова", Left = 268, Top = 280, Width = 80 };
            btnResetHotkey.Click += (s, e) =>
            {
                capturedHotkeyModifiers = HotkeyUtil.MOD_CONTROL | HotkeyUtil.MOD_ALT;
                capturedHotkeyVk = HotkeyUtil.DefaultVk;
                txtHotkey.Text = HotkeyUtil.Format(capturedHotkeyModifiers, capturedHotkeyVk);
            };

            var btnClearHotkey = new Button { Text = "Вимкнути", Left = 352, Top = 280, Width = 80 };
            btnClearHotkey.Click += (s, e) =>
            {
                capturedHotkeyModifiers = 0;
                capturedHotkeyVk = 0;
                txtHotkey.Text = HotkeyUtil.Format(0, 0);
            };
            var lblHotkeyHint = Hint(12, 312, 34, "Клацніть у поле й натисніть бажану комбінацію (потрібен хоча б один Ctrl/Alt/Shift).");

            var lblTheme = new Label { Text = "Тема оформлення:", Left = 12, Top = 352, Width = 440 };
            radioThemeLight = new RadioButton { Text = "Світла", Left = 12, Top = 374, Width = 100 };
            radioThemeDark = new RadioButton { Text = "Темна", Left = 120, Top = 374, Width = 100 };
            radioThemeBlue = new RadioButton { Text = "Синя", Left = 228, Top = 374, Width = 100 };
            if (working.Theme == ThemeManager.DarkThemeName) radioThemeDark.Checked = true;
            else if (working.Theme == ThemeManager.BlueThemeName) radioThemeBlue.Checked = true;
            else radioThemeLight.Checked = true;
            EventHandler onThemePicked = (s, e) => ThemeManager.Apply(this, SelectedThemeName());
            radioThemeLight.CheckedChanged += onThemePicked;
            radioThemeDark.CheckedChanged += onThemePicked;
            radioThemeBlue.CheckedChanged += onThemePicked;

            chkMica = new CheckBox
            {
                Text = "Прозорий фон Mica (Windows 11, версія 22H2+)",
                Left = 12,
                Top = 404,
                Width = 440,
                Checked = working.MicaEnabled
            };
            var lblMicaHint = Hint(12, 426, 34,
                "Розмитий фон верхньої панелі й статус-рядка. На старіших версіях Windows пункт просто не матиме ефекту.");

            // ---- Права колонка: папка налаштувань, облікові дані Windows, автозапуск, попередження RDP ----
            var lblSettingsFolder = new Label { Text = "Папка для збереження налаштувань і паролів:", Left = RightCol, Top = 14, Width = 440 };
            txtSettingsFolder = new TextBox { Left = RightCol, Top = 36, Width = 250, Text = working.SettingsFolder };
            var btnBrowseSettings = new Button { Text = "Огляд...", Left = RightCol + 258, Top = 34, Width = 90 };
            btnBrowseSettings.Click += (s, e) => BrowseInto(txtSettingsFolder);
            var btnDefaultSettings = new Button { Text = "Типова", Left = RightCol + 352, Top = 34, Width = 88 };
            btnDefaultSettings.Click += (s, e) => txtSettingsFolder.Text = "";
            var lblSettingsHint = Hint(RightCol, 64, 66,
                "Наприклад, папка на Google Drive -- тоді паролі й налаштування доступні на всіх ваших ПК. " +
                "Файл RdpConsole.dat зашифровано, прочитати його може лише RDP Console. " +
                "Порожньо -- " + SettingsManager.LocalDir + ".");

            chkStoreInWindows = new CheckBox
            {
                Text = "Під час підключення записувати пароль в облікові дані Windows",
                Left = RightCol,
                Top = 138,
                Width = 440,
                Checked = working.StoreInWindowsCredentials
            };
            var lblStoreHint = Hint(RightCol, 160, 50,
                "Пароль із застосунку зберігається в Диспетчері облікових даних (TERMSRV/<адреса>) замість тимчасового файлу; " +
                "далі підключення позначене W. Потрібен користувач у .rdp.");

            chkAutostart = new CheckBox
            {
                Text = "Запускати разом із Windows (згорнутим у трей)",
                Left = RightCol,
                Top = 220,
                Width = 440,
                Checked = AutostartManager.IsEnabled()
            };

            var lblRdpWarning = new Label
            {
                Text = "Попередження \"Невідоме віддалене підключення\" (оновлення Windows, квітень 2026):",
                Left = RightCol,
                Top = 260,
                Width = 440,
                Height = 34
            };
            lblRdpWarningState = new Label { Left = RightCol, Top = 298, Width = 300, Height = 20 };
            btnRdpWarning = new Button { Left = RightCol + 308, Top = 294, Width = 132 };
            btnRdpWarning.Click += (s, e) => ToggleRdpWarning();
            var lblRdpWarningHint = Hint(RightCol, 324, 50,
                "Параметр RedirectionWarningDialogVersion = 1 повертає попередню поведінку mstsc. " +
                "Застосовується до всіх користувачів ПК, потрібні права адміністратора.");
            UpdateRdpWarningState();

            var btnOk = new Button { Text = "OK", Left = 740, Top = 488, Width = 85 };
            btnOk.Click += (s, e) => Accept();
            var btnCancel = new Button { Text = "Скасувати", Left = 832, Top = 488, Width = 85, DialogResult = DialogResult.Cancel };

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.AddRange(new Control[]
            {
                lblRoot, txtRoot, btnBrowse,
                lblDefaults, lvDefaults, btnAddDefault, btnEditDefault, btnDelDefault, lblDefaultsHint,
                lblHotkey, txtHotkey, btnResetHotkey, btnClearHotkey, lblHotkeyHint,
                lblTheme, radioThemeLight, radioThemeDark, radioThemeBlue, chkMica, lblMicaHint,
                lblSettingsFolder, txtSettingsFolder, btnBrowseSettings, btnDefaultSettings, lblSettingsHint,
                chkStoreInWindows, lblStoreHint, chkAutostart,
                lblRdpWarning, lblRdpWarningState, btnRdpWarning, lblRdpWarningHint,
                btnOk, btnCancel
            });

            ThemeManager.Apply(this, working.Theme);
        }

        void BrowseInto(TextBox target)
        {
            using (var fbd = new FolderBrowserDialog { SelectedPath = SafeDir(target.Text) })
            {
                if (fbd.ShowDialog(this) == DialogResult.OK) target.Text = fbd.SelectedPath;
            }
        }

        void FillDefaults()
        {
            lvDefaults.BeginUpdate();
            lvDefaults.Items.Clear();
            foreach (var d in working.DefaultCredentials)
            {
                var item = new ListViewItem(string.IsNullOrWhiteSpace(d.User) ? "(будь-який)" : d.User);
                item.SubItems.Add("••••••••");
                lvDefaults.Items.Add(item);
            }
            lvDefaults.EndUpdate();
        }

        void AddDefault()
        {
            using (var dlg = new PasswordDialog("Облікові дані за замовчуванням", SelectedThemeName(), true))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                working.DefaultCredentials.Add(new DefaultCredential { User = dlg.User, Password = dlg.Password });
                FillDefaults();
            }
        }

        void EditDefault()
        {
            if (lvDefaults.SelectedIndices.Count == 0) return;
            var d = working.DefaultCredentials[lvDefaults.SelectedIndices[0]];
            using (var dlg = new PasswordDialog("Облікові дані за замовчуванням", SelectedThemeName(), true, d.User, d.Password))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                d.User = dlg.User;
                d.Password = dlg.Password;
                FillDefaults();
            }
        }

        void UpdateRdpWarningState()
        {
            bool legacy = RdpWarningPolicy.IsLegacyDialogEnabled();
            lblRdpWarningState.Text = legacy
                ? "Стан: вимкнено (параметр прописано)"
                : "Стан: показується (параметр не прописано)";
            btnRdpWarning.Text = legacy ? "Прибрати" : "Прописати";
        }

        // Діє одразу (як і запит UAC), а не після OK -- це системний параметр, а не
        // налаштування застосунку, тож "Скасувати" його не відкочує.
        void ToggleRdpWarning()
        {
            bool enable = !RdpWarningPolicy.IsLegacyDialogEnabled();
            if (!RdpWarningPolicy.SetLegacyDialog(enable))
            {
                MessageBox.Show(this, "Не вдалося змінити параметр (потрібні права адміністратора).",
                    "Налаштування", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            UpdateRdpWarningState();
        }

        string SelectedThemeName()
        {
            if (radioThemeDark.Checked) return ThemeManager.DarkThemeName;
            if (radioThemeBlue.Checked) return ThemeManager.BlueThemeName;
            return ThemeManager.LightThemeName;
        }

        void TxtHotkey_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;

            if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.Menu ||
                e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin)
            {
                return; // чекаємо на клавішу, що не є самим модифікатором
            }

            if (e.KeyCode == Keys.Escape)
            {
                txtHotkey.Text = HotkeyUtil.Format(capturedHotkeyModifiers, capturedHotkeyVk);
                return;
            }

            if (!e.Control && !e.Alt && !e.Shift)
            {
                txtHotkey.Text = "Потрібен хоча б один із Ctrl / Alt / Shift...";
                return;
            }

            uint mods = 0;
            if (e.Control) mods |= HotkeyUtil.MOD_CONTROL;
            if (e.Alt) mods |= HotkeyUtil.MOD_ALT;
            if (e.Shift) mods |= HotkeyUtil.MOD_SHIFT;

            capturedHotkeyModifiers = mods;
            capturedHotkeyVk = (uint)e.KeyCode;
            txtHotkey.Text = HotkeyUtil.Format(capturedHotkeyModifiers, capturedHotkeyVk);
        }

        static string SafeDir(string p)
        {
            try { return Directory.Exists(p) ? p : ""; } catch { return ""; }
        }

        void Accept()
        {
            var root = txtRoot.Text.Trim();
            if (root.Length > 0 && !Directory.Exists(root))
            {
                var r = MessageBox.Show(this, "Вказана папка з файлами *.rdp не існує. Зберегти все одно?", "Перевірка",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes) return;
            }

            var settingsFolder = txtSettingsFolder.Text.Trim();
            if (settingsFolder.Length > 0 && !Directory.Exists(settingsFolder))
            {
                MessageBox.Show(this, "Вказана папка для збереження налаштувань не існує.", "Перевірка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            working.RootFolder = root;
            working.SettingsFolder = settingsFolder;
            working.HotkeyModifiers = capturedHotkeyModifiers;
            working.HotkeyKey = capturedHotkeyVk;
            working.Theme = SelectedThemeName();
            working.MicaEnabled = chkMica.Checked;
            working.StoreInWindowsCredentials = chkStoreInWindows.Checked;

            if (chkAutostart.Checked != AutostartManager.IsEnabled())
            {
                try { AutostartManager.SetEnabled(chkAutostart.Checked); }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Не вдалося змінити автозапуск:\n" + ex.Message, "Налаштування",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            ResultSettings = working;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    public static class Program
    {
        public const string AppVersion = "1.9.0";
        public const string RepoUrl = "https://github.com/Kolp305/RdpConsole";

        // Унікальне для цього застосунку зареєстроване Windows-повідомлення: перший
        // (уже запущений) екземпляр слухає його у WndProc і показує своє вікно.
        public static readonly int WM_SHOWME = RegisterWindowMessage("RdpConsole_ShowMainWindow_v1");

        const string MutexName = "Local\\RdpConsole_SingleInstance_8F3E2B1C-6B7A-4C2E-9C7C-1E5A2B7D4F10";
        const int HWND_BROADCAST = 0xffff;

        [DllImport("user32.dll")]
        static extern int RegisterWindowMessage(string message);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [STAThread]
        public static void Main(string[] args)
        {
            bool createdNew;
            using (var mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    // Застосунок уже запущено -- просимо той екземпляр показати своє
                    // вікно (замість запуску другого, з другою іконкою в треї) і виходимо.
                    PostMessage((IntPtr)HWND_BROADCAST, WM_SHOWME, IntPtr.Zero, IntPtr.Zero);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Application.ThreadException += (s, e) =>
                    MessageBox.Show("Неочікувана помилка:\n" + e.Exception.Message, "RDP Console",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);

                bool startMinimized = args.Any(a => string.Equals(a, AutostartManager.MinimizedArg, StringComparison.OrdinalIgnoreCase));
                Application.Run(new MainForm(startMinimized));
                GC.KeepAlive(mutex);
            }
        }
    }
}
