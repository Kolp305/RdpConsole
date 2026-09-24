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

        // Повертає true, якщо Mica увімкнено -- у цьому разі контрол(и), де має
        // "просвічувати" фон, потрібно пофарбувати в чистий чорний (Color.Black):
        // під час DwmExtendFrameIntoClientArea з від'ємними полями DWM трактує
        // такі пікселі як прозорі й показує крізь них розмитий матеріал Mica.
        public static bool TryEnable(IntPtr hwnd)
        {
            try
            {
                int dark = 0;
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
    }

    [DataContract]
    public class AppSettings
    {
        [DataMember] public string RootFolder { get; set; }
        [DataMember] public int WindowWidth { get; set; }
        [DataMember] public int WindowHeight { get; set; }
        [DataMember] public Dictionary<string, string> EncryptedPasswords { get; set; }
        [DataMember] public string DefaultEncryptedPassword { get; set; }
        [DataMember] public bool HierarchyView { get; set; }
        [DataMember] public uint HotkeyModifiers { get; set; }
        [DataMember] public uint HotkeyKey { get; set; }

        public static AppSettings CreateDefault()
        {
            return new AppSettings
            {
                RootFolder = "",
                WindowWidth = 940,
                WindowHeight = 620,
                EncryptedPasswords = new Dictionary<string, string>(),
                HierarchyView = true,
                HotkeyModifiers = HotkeyUtil.MOD_CONTROL | HotkeyUtil.MOD_ALT,
                HotkeyKey = HotkeyUtil.DefaultVk
            };
        }
    }

    public static class SettingsManager
    {
        static string SettingsDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RdpConsole"); }
        }

        static string SettingsPath
        {
            get { return Path.Combine(SettingsDir, "settings.json"); }
        }

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    using (var fs = File.OpenRead(SettingsPath))
                    {
                        var ser = new DataContractJsonSerializer(typeof(AppSettings));
                        var s = (AppSettings)ser.ReadObject(fs);
                        if (s.EncryptedPasswords == null) s.EncryptedPasswords = new Dictionary<string, string>();
                        // Старі файли налаштувань (до появи цієї функції) не мають цих полів --
                        // після десеріалізації вони будуть 0, повертаємо типову комбінацію.
                        if (s.HotkeyKey == 0)
                        {
                            s.HotkeyModifiers = HotkeyUtil.MOD_CONTROL | HotkeyUtil.MOD_ALT;
                            s.HotkeyKey = HotkeyUtil.DefaultVk;
                        }
                        return s;
                    }
                }
            }
            catch
            {
                // повернемо типові налаштування, якщо файл пошкоджено
            }
            return AppSettings.CreateDefault();
        }

        public static void Save(AppSettings s)
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                using (var fs = File.Create(SettingsPath))
                {
                    var ser = new DataContractJsonSerializer(typeof(AppSettings));
                    ser.WriteObject(fs, s);
                }
            }
            catch
            {
                // тихо ігноруємо помилки збереження (наприклад, немає прав на запис)
            }
        }
    }

    // Пароль шифрується за допомогою Windows DPAPI (прив'язка до поточного облікового
    // запису Windows на цій машині) -- так само, як зберігають майстер-паролі інші
    // менеджери підключень (наприклад, mRemoteNG). Ніхто інший, ані на цьому, ані на
    // іншому комп'ютері, розшифрувати збережений пароль не зможе.
    public static class PasswordManager
    {
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("RdpConsole.v1.pwd");

        public static string Encrypt(string plainPassword)
        {
            var bytes = Encoding.UTF8.GetBytes(plainPassword);
            var protectedBytes = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }

        public static string Decrypt(string encryptedBase64)
        {
            try
            {
                var protectedBytes = Convert.FromBase64String(encryptedBase64);
                var bytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return null;
            }
        }

        // Пароль конкретного підключення має пріоритет; якщо для нього нічого не
        // збережено -- використовується пароль за замовчуванням із налаштувань (якщо є).
        public static string ResolveEffectivePassword(AppSettings settings, string entryFullPath)
        {
            string encrypted;
            if (settings.EncryptedPasswords == null || !settings.EncryptedPasswords.TryGetValue(entryFullPath, out encrypted))
            {
                encrypted = settings.DefaultEncryptedPassword;
            }
            return string.IsNullOrEmpty(encrypted) ? null : Decrypt(encrypted);
        }
    }

    public class RdpEntry
    {
        public string FullPath;
        public string DisplayName;
        public string RelativeFolder;

        public override string ToString()
        {
            return RelativeFolder == "(корінь)" ? DisplayName : RelativeFolder + " \\ " + DisplayName;
        }
    }

    public static class RdpScanner
    {
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
                result.Add(new RdpEntry
                {
                    FullPath = f,
                    DisplayName = Path.GetFileNameWithoutExtension(f),
                    RelativeFolder = string.IsNullOrEmpty(dir) ? "(корінь)" : dir
                });
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
        public static void Populate(TreeView tree, List<RdpEntry> entries, Func<RdpEntry, string> formatLabel)
        {
            tree.BeginUpdate();
            tree.Nodes.Clear();

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

            AddLevel(tree.Nodes, "", byFolder, formatLabel);
            tree.EndUpdate();
        }

        static void AddLevel(TreeNodeCollection parentNodes, string currentPath, Dictionary<string, List<RdpEntry>> byFolder,
            Func<RdpEntry, string> formatLabel)
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
                var node = new TreeNode(name)
                {
                    ImageIndex = ShellIcons.FolderImageIndex,
                    SelectedImageIndex = ShellIcons.FolderImageIndex
                };
                parentNodes.Add(node);
                AddLevel(node.Nodes, subfolderPath, byFolder, formatLabel);
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
                    var pwd = PasswordManager.ResolveEffectivePassword(settings, t.FullPath);
                    if (!string.IsNullOrEmpty(pwd))
                    {
                        try { Clipboard.SetText(pwd); } catch { }
                    }

                    var connectPath = PrepareConnectionFile(t.FullPath, pwd);
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
        static string PrepareConnectionFile(string originalPath, string password)
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

                if (!string.IsNullOrEmpty(password))
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
                    lines.Add("password 51:b:" + EncryptForMstsc(password));
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

    public class MainForm : Form
    {
        AppSettings settings;
        List<RdpEntry> allEntries = new List<RdpEntry>();

        TextBox txtSearch;
        Button btnExit;
        Button btnSettings;
        Button btnRefresh;
        Button btnViewMode;
        LinkLabel lnkVersion;
        ListView listView;
        TreeView treeView;
        StatusStrip statusStrip;
        ToolStripStatusLabel statusLabel;
        ContextMenuStrip itemMenu;
        Panel topPanel;
        Button btnRepo;
        bool micaEnabled;

        NotifyIcon trayIcon;
        bool reallyExit;
        bool trayHintShown;

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
        // з'являтися без фокуса -- і миттєво закриватися через Deactivate. Стандартний обхід:
        // імітувати натискання Alt перед SetForegroundWindow -- Windows знімає обмеження,
        // якщо останньою дією користувача була системна клавіша.
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        const byte VK_MENU = 0x12;
        const uint KEYEVENTF_KEYUP = 0x0002;

        static void ForceForegroundWindow(IntPtr hWnd)
        {
            keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
            SetForegroundWindow(hWnd);
            keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public MainForm()
        {
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
            var form = new TraySearchForm(allEntries, settings, this, () => ExitApplication());

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
            topPanel = new Panel { Dock = DockStyle.Top, Height = 36, Padding = new Padding(6, 5, 6, 3) };

            // Стиль версії/посилання на репозиторій -- як у ShadowSessionTool: маленька
            // намальована іконка-ланцюжок (а не емодзі, яке по-різному рендериться) і
            // підкреслений текст версії, що виглядає як гіперпосилання.
            btnRepo = new Button
            {
                Dock = DockStyle.Left,
                Width = 26,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
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

            txtSearch = new TextBox { Dock = DockStyle.Left, Width = 320 };
            SetPlaceholder(txtSearch, "Пошук підключення...");
            txtSearch.TextChanged += (s, e) => { ClearPlaceholderState(); ApplyFilter(); };
            txtSearch.KeyDown += TxtSearch_KeyDown;

            btnRefresh = new Button { Dock = DockStyle.Right, Width = 100, Text = "Оновити (F5)" };
            btnRefresh.Click += (s, e) => RescanAndFill();

            btnSettings = new Button { Dock = DockStyle.Right, Width = 110, Text = "Налаштування" };
            btnSettings.Click += (s, e) => OpenSettings();

            btnExit = new Button { Dock = DockStyle.Right, Width = 150, Text = "Завершити програму" };
            btnExit.Click += (s, e) => ExitApplication();

            btnViewMode = new Button { Dock = DockStyle.Right, Width = 100 };
            btnViewMode.Click += (s, e) =>
            {
                settings.HierarchyView = !settings.HierarchyView;
                SettingsManager.Save(settings);
                ApplyFilter();
            };

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
                ShowGroups = true
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
                if (e.Node.Tag is RdpEntry) itemMenu.Show(treeView, e.Location);
            };
            treeView.KeyDown += (s, e) =>
            {
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

        void MainForm_Load(object sender, EventArgs e)
        {
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
                    SettingsManager.Save(settings);
                }
            }
        }

        void RescanAndFill()
        {
            allEntries = RdpScanner.Scan(settings.RootFolder);
            ApplyFilter();
        }

        bool HasOwnPassword(RdpEntry entry)
        {
            return settings.EncryptedPasswords != null && settings.EncryptedPasswords.ContainsKey(entry.FullPath);
        }

        bool HasEffectivePassword(RdpEntry entry)
        {
            return HasOwnPassword(entry) || !string.IsNullOrEmpty(settings.DefaultEncryptedPassword);
        }

        // "✓" -- для цього підключення збережено власний пароль;
        // "•" -- власного немає, але спрацює пароль за замовчуванням.
        string PasswordMarker(RdpEntry entry)
        {
            if (HasOwnPassword(entry)) return "✓";
            if (!string.IsNullOrEmpty(settings.DefaultEncryptedPassword)) return "•";
            return "";
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

            IEnumerable<RdpEntry> src = allEntries;
            if (filter.Length > 0)
            {
                src = allEntries.Where(x =>
                    x.DisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    x.RelativeFolder.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            var groups = new Dictionary<string, ListViewGroup>();
            int shown = 0;
            foreach (var entry in src)
            {
                ListViewGroup g;
                if (!groups.TryGetValue(entry.RelativeFolder, out g))
                {
                    g = new ListViewGroup(entry.RelativeFolder, entry.RelativeFolder);
                    groups[entry.RelativeFolder] = g;
                    listView.Groups.Add(g);
                }
                var item = new ListViewItem(entry.DisplayName);
                item.SubItems.Add(PasswordMarker(entry));
                item.Tag = entry;
                item.Group = g;
                item.ToolTipText = entry.FullPath;
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
            RdpTreeBuilder.Populate(treeView, allEntries, entry =>
            {
                var marker = PasswordMarker(entry);
                return marker.Length == 0 ? entry.DisplayName : entry.DisplayName + "   " + marker;
            });
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
            if (hit != null)
            {
                itemMenu.Show(listView, e.Location);
            }
        }

        void ListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                ConnectSelected();
                e.Handled = true;
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
            var targets = SelectedEntries();
            itemMenu.Items.Clear();

            if (targets.Count == 0)
            {
                e.Cancel = true;
                return;
            }

            var miConnect = new ToolStripMenuItem("Підключитися");
            miConnect.Font = new Font(itemMenu.Font, FontStyle.Bold);
            miConnect.Click += (s, ev) => Launcher.Connect(this, settings, targets);
            itemMenu.Items.Add(miConnect);

            itemMenu.Items.Add(new ToolStripSeparator());

            if (targets.Count == 1)
            {
                var t = targets[0];
                bool hasOwnPwd = HasOwnPassword(t);

                var miPwd = new ToolStripMenuItem(hasOwnPwd ? "Змінити пароль..." : "Зберегти пароль...");
                miPwd.Click += (s, ev) => SavePasswordFor(t);
                itemMenu.Items.Add(miPwd);

                var miCopyPwd = new ToolStripMenuItem("Копіювати пароль") { Enabled = HasEffectivePassword(t) };
                miCopyPwd.Click += (s, ev) => CopyPasswordFor(t);
                itemMenu.Items.Add(miCopyPwd);

                if (hasOwnPwd)
                {
                    var miDelPwd = new ToolStripMenuItem("Видалити пароль");
                    miDelPwd.Click += (s, ev) => DeletePasswordFor(t);
                    itemMenu.Items.Add(miDelPwd);
                }

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

        void SavePasswordFor(RdpEntry entry)
        {
            using (var dlg = new PasswordDialog(entry.DisplayName))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (settings.EncryptedPasswords == null) settings.EncryptedPasswords = new Dictionary<string, string>();
                    settings.EncryptedPasswords[entry.FullPath] = PasswordManager.Encrypt(dlg.Password);
                    SettingsManager.Save(settings);
                    ApplyFilter();
                }
            }
        }

        void CopyPasswordFor(RdpEntry entry)
        {
            var pwd = PasswordManager.ResolveEffectivePassword(settings, entry.FullPath);
            if (string.IsNullOrEmpty(pwd)) return;

            try { Clipboard.SetText(pwd); } catch { }
        }

        void DeletePasswordFor(RdpEntry entry)
        {
            if (settings.EncryptedPasswords == null) return;
            if (settings.EncryptedPasswords.Remove(entry.FullPath))
            {
                SettingsManager.Save(settings);
                ApplyFilter();
            }
        }

        void OpenSettings()
        {
            using (var f = new SettingsForm(settings))
            {
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    bool hotkeyChanged = settings.HotkeyModifiers != f.ResultSettings.HotkeyModifiers ||
                                          settings.HotkeyKey != f.ResultSettings.HotkeyKey;
                    settings = f.ResultSettings;
                    SettingsManager.Save(settings);
                    RescanAndFill();

                    if (hotkeyChanged)
                    {
                        UnregisterGlobalHotkey();
                        RegisterGlobalHotkey();
                    }
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
            SettingsManager.Save(settings);
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

        // Windows 11 Mica -- напівпрозорий розмитий фон вікна (як у застосунках
        // Параметрів). На старіших системах TryEnable поверне false і форма
        // просто залишиться зі звичайним непрозорим фоном.
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            micaEnabled = DwmMica.TryEnable(Handle);
            if (micaEnabled)
            {
                // Чистий чорний -- "прозорий" для DWM під час sheet-of-glass розширення;
                // саме верхня панель і статус-рядок показують ефект Mica, список
                // підключень лишається непрозорим (звичайний фон) для читабельності.
                topPanel.BackColor = Color.Black;
                statusStrip.BackColor = Color.Black;
                statusLabel.ForeColor = Color.FromArgb(235, 235, 235);
                lnkVersion.BackColor = Color.Transparent;
                lnkVersion.LinkColor = Color.FromArgb(210, 210, 210);
                btnRepo.BackColor = Color.Transparent;
                btnRepo.Image = CreateLinkIcon(Color.FromArgb(210, 210, 210));
            }
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

        const int EdgeMargin = 8;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Чистий чорний фон форми -- "прозорий" для DWM Mica; вузька смужка
            // навколо результатів пошуку (EdgeMargin) показує розмитий матеріал.
            if (DwmMica.TryEnable(Handle)) BackColor = Color.Black;
        }

        public TraySearchForm(List<RdpEntry> allEntries, AppSettings settings, IWin32Window connectOwner, Action exitAction)
        {
            this.allEntries = allEntries;
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
                SettingsManager.Save(settings);
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
                RdpTreeBuilder.Populate(treeResults, allEntries, null);
                if (treeResults.Nodes.Count > 0) treeResults.SelectedNode = treeResults.Nodes[0];
                return;
            }

            IEnumerable<RdpEntry> matches = allEntries;
            if (filter.Length > 0)
            {
                matches = allEntries.Where(x =>
                    x.DisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    x.RelativeFolder.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            lstResults.BeginUpdate();
            lstResults.Items.Clear();
            foreach (var entry in matches
                         .OrderBy(e => e.RelativeFolder, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase))
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

        void ConnectSelected()
        {
            RdpEntry entry = treeResults.Visible
                ? (treeResults.SelectedNode != null ? treeResults.SelectedNode.Tag as RdpEntry : null)
                : lstResults.SelectedItem as RdpEntry;
            if (entry == null) return;

            Launcher.Connect(connectOwner, settings, new[] { entry });
            Close();
        }
    }

    public class PasswordDialog : Form
    {
        public string Password;

        TextBox txtPassword;
        CheckBox chkShow;

        public PasswordDialog(string forName)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = "Пароль для \"" + forName + "\"";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Width = 380;
            Height = 170;
            Font = new Font("Segoe UI", 9.5f);

            var lbl = new Label { Text = "Пароль:", Left = 12, Top = 16, Width = 340 };
            txtPassword = new TextBox { Left = 12, Top = 38, Width = 340, UseSystemPasswordChar = true };

            chkShow = new CheckBox { Text = "Показати пароль", Left = 12, Top = 66, Width = 200 };
            chkShow.CheckedChanged += (s, e) => txtPassword.UseSystemPasswordChar = !chkShow.Checked;

            var btnOk = new Button { Text = "Зберегти", Left = 172, Top = 96, Width = 85 };
            btnOk.Click += (s, e) => Accept();
            var btnCancel = new Button { Text = "Скасувати", Left = 264, Top = 96, Width = 85, DialogResult = DialogResult.Cancel };

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.Add(lbl);
            Controls.Add(txtPassword);
            Controls.Add(chkShow);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            Load += (s, e) => txtPassword.Focus();
        }

        void Accept()
        {
            if (string.IsNullOrEmpty(txtPassword.Text))
            {
                MessageBox.Show(this, "Введіть пароль.", "Перевірка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Password = txtPassword.Text;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    public class SettingsForm : Form
    {
        AppSettings working;
        public AppSettings ResultSettings;

        TextBox txtRoot;
        TextBox txtDefaultPassword;
        CheckBox chkShowDefaultPassword;
        CheckBox chkClearDefaultPassword;
        TextBox txtHotkey;
        uint capturedHotkeyModifiers;
        uint capturedHotkeyVk;

        public SettingsForm(AppSettings current)
        {
            AutoScaleMode = AutoScaleMode.None;
            working = Clone(current);

            Text = "Налаштування";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Width = 480;
            Height = 420;
            Font = new Font("Segoe UI", 9.5f);

            capturedHotkeyModifiers = working.HotkeyModifiers;
            capturedHotkeyVk = working.HotkeyKey;

            BuildUi();
        }

        static AppSettings Clone(AppSettings s)
        {
            return new AppSettings
            {
                RootFolder = s.RootFolder,
                WindowWidth = s.WindowWidth,
                WindowHeight = s.WindowHeight,
                DefaultEncryptedPassword = s.DefaultEncryptedPassword,
                HierarchyView = s.HierarchyView,
                HotkeyModifiers = s.HotkeyModifiers,
                HotkeyKey = s.HotkeyKey,
                EncryptedPasswords = s.EncryptedPasswords != null
                    ? new Dictionary<string, string>(s.EncryptedPasswords)
                    : new Dictionary<string, string>()
            };
        }

        void BuildUi()
        {
            var lblRoot = new Label { Text = "Папка з файлами *.rdp:", Left = 12, Top = 14, Width = 300 };
            txtRoot = new TextBox { Left = 12, Top = 36, Width = 340, Text = working.RootFolder };
            var btnBrowse = new Button { Text = "Огляд...", Left = 360, Top = 34, Width = 90 };
            btnBrowse.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog { SelectedPath = SafeDir(txtRoot.Text) })
                {
                    if (fbd.ShowDialog(this) == DialogResult.OK) txtRoot.Text = fbd.SelectedPath;
                }
            };

            var lblDefaultPwd = new Label
            {
                Text = "Пароль за замовчуванням (використовується, якщо для конкретного підключення власний пароль не збережено):",
                Left = 12,
                Top = 72,
                Width = 440,
                Height = 32
            };
            txtDefaultPassword = new TextBox { Left = 12, Top = 106, Width = 340, UseSystemPasswordChar = true };
            chkShowDefaultPassword = new CheckBox { Text = "Показати", Left = 360, Top = 108, Width = 90 };
            chkShowDefaultPassword.CheckedChanged += (s, e) => txtDefaultPassword.UseSystemPasswordChar = !chkShowDefaultPassword.Checked;

            chkClearDefaultPassword = new CheckBox
            {
                Text = "Прибрати пароль за замовчуванням",
                Left = 12,
                Top = 134,
                Width = 300,
                Enabled = !string.IsNullOrEmpty(working.DefaultEncryptedPassword)
            };

            var lblHint = new Label
            {
                Left = 12,
                Top = 160,
                Width = 440,
                Height = 32,
                ForeColor = SystemColors.GrayText,
                Text = "Залиште поле порожнім, щоб не змінювати вже збережений пароль за замовчуванням."
            };

            var lblHotkey = new Label { Text = "Гаряча клавіша (відкриває вікно пошуку з будь-якого місця):", Left = 12, Top = 198, Width = 440 };

            txtHotkey = new TextBox
            {
                Left = 12,
                Top = 220,
                Width = 250,
                ReadOnly = true,
                Text = HotkeyUtil.Format(capturedHotkeyModifiers, capturedHotkeyVk)
            };
            txtHotkey.KeyDown += TxtHotkey_KeyDown;

            var btnResetHotkey = new Button { Text = "Типова", Left = 268, Top = 218, Width = 80 };
            btnResetHotkey.Click += (s, e) =>
            {
                capturedHotkeyModifiers = HotkeyUtil.MOD_CONTROL | HotkeyUtil.MOD_ALT;
                capturedHotkeyVk = HotkeyUtil.DefaultVk;
                txtHotkey.Text = HotkeyUtil.Format(capturedHotkeyModifiers, capturedHotkeyVk);
            };

            var btnClearHotkey = new Button { Text = "Вимкнути", Left = 352, Top = 218, Width = 80 };
            btnClearHotkey.Click += (s, e) =>
            {
                capturedHotkeyModifiers = 0;
                capturedHotkeyVk = 0;
                txtHotkey.Text = HotkeyUtil.Format(0, 0);
            };

            var lblHotkeyHint = new Label
            {
                Left = 12,
                Top = 250,
                Width = 440,
                Height = 32,
                ForeColor = SystemColors.GrayText,
                Text = "Клацніть у поле й натисніть бажану комбінацію (потрібен хоча б один Ctrl/Alt/Shift)."
            };

            var btnOk = new Button { Text = "OK", Left = 280, Top = 336, Width = 85 };
            btnOk.Click += (s, e) => Accept();
            var btnCancel = new Button { Text = "Скасувати", Left = 372, Top = 336, Width = 85, DialogResult = DialogResult.Cancel };

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.Add(lblRoot);
            Controls.Add(txtRoot);
            Controls.Add(btnBrowse);
            Controls.Add(lblDefaultPwd);
            Controls.Add(txtDefaultPassword);
            Controls.Add(chkShowDefaultPassword);
            Controls.Add(chkClearDefaultPassword);
            Controls.Add(lblHint);
            Controls.Add(lblHotkey);
            Controls.Add(txtHotkey);
            Controls.Add(btnResetHotkey);
            Controls.Add(btnClearHotkey);
            Controls.Add(lblHotkeyHint);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
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
                var r = MessageBox.Show(this, "Вказана папка не існує. Зберегти все одно?", "Перевірка",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes) return;
            }

            if (chkClearDefaultPassword.Checked)
            {
                working.DefaultEncryptedPassword = null;
            }
            else if (!string.IsNullOrEmpty(txtDefaultPassword.Text))
            {
                working.DefaultEncryptedPassword = PasswordManager.Encrypt(txtDefaultPassword.Text);
            }

            working.RootFolder = root;
            working.HotkeyModifiers = capturedHotkeyModifiers;
            working.HotkeyKey = capturedHotkeyVk;
            ResultSettings = working;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    public static class Program
    {
        public const string AppVersion = "1.2.1";
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
        public static void Main()
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

                Application.Run(new MainForm());
                GC.KeepAlive(mutex);
            }
        }
    }
}
