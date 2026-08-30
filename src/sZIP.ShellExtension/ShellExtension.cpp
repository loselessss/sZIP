#include <windows.h>
#include <shobjidl.h>
#include <shlwapi.h>
#include <algorithm>
#include <atomic>
#include <cwctype>
#include <new>
#include <string>
#include <vector>

#pragma comment(lib, "ole32.lib")
#pragma comment(lib, "shell32.lib")
#pragma comment(lib, "shlwapi.lib")
#pragma comment(lib, "advapi32.lib")

#define RETURN_IF_FAILED(expression) do { const HRESULT result = (expression); if (FAILED(result)) return result; } while (false)

namespace
{
    const CLSID CLSID_sZIPMenu =
        {0x5fd30db9, 0xb45b, 0x48dd, {0xa3, 0x59, 0x30, 0xbe, 0xea, 0x4c, 0xa4, 0xe1}};
    const CLSID CLSID_LegacyExtractDirect =
        {0xf4da9d54, 0x7593, 0x4d8a, {0xa8, 0x58, 0xa8, 0xf2, 0x70, 0x22, 0xfe, 0xd2}};
    const CLSID CLSID_LegacyExtractSmart =
        {0xc98ac2e7, 0xa656, 0x472e, {0xbf, 0x79, 0x3f, 0xdd, 0xa6, 0xa0, 0x62, 0x1e}};

    enum class CommandKind
    {
        Root,
        SmartExtract,
        ExtractHere,
        Open,
        CompressZip,
        CompressSevenZip,
        CompressDialog
    };

    std::atomic<long> g_objectCount = 0;
    HMODULE g_module = nullptr;

    HRESULT CopyResult(const std::wstring& value, PWSTR* result)
    {
        if (!result) return E_POINTER;
        return SHStrDupW(value.c_str(), result);
    }

    bool IsKorean()
    {
        wchar_t language[16] = {};
        DWORD languageSize = sizeof(language);
        if (RegGetValueW(HKEY_CURRENT_USER, L"Software\\sZIP", L"Language",
                RRF_RT_REG_SZ, nullptr, language, &languageSize) == ERROR_SUCCESS)
        {
            if (_wcsicmp(language, L"ko") == 0) return true;
            if (_wcsicmp(language, L"en") == 0) return false;
        }
        return PRIMARYLANGID(GetUserDefaultUILanguage()) == LANG_KOREAN;
    }

    std::wstring ModuleDirectory()
    {
        wchar_t path[MAX_PATH] = {};
        GetModuleFileNameW(g_module, path, ARRAYSIZE(path));
        PathRemoveFileSpecW(path);
        return path;
    }

    std::wstring Quote(const std::wstring& value)
    {
        return L"\"" + value + L"\"";
    }

    bool IsSupportedArchive(const std::wstring& path)
    {
        const auto dot = path.find_last_of(L'.');
        if (dot == std::wstring::npos) return false;
        std::wstring extension = path.substr(dot);
        for (auto& character : extension) character = static_cast<wchar_t>(towlower(character));
        return extension == L".zip" || extension == L".7z" || extension == L".rar"
            || extension == L".tar" || extension == L".gz" || extension == L".tgz";
    }

    HRESULT SelectedPaths(IShellItemArray* selection, std::vector<std::wstring>& paths)
    {
        if (!selection) return E_INVALIDARG;
        DWORD count = 0;
        RETURN_IF_FAILED(selection->GetCount(&count));
        for (DWORD index = 0; index < count; ++index)
        {
            IShellItem* item = nullptr;
            RETURN_IF_FAILED(selection->GetItemAt(index, &item));
            PWSTR path = nullptr;
            const HRESULT result = item->GetDisplayName(SIGDN_FILESYSPATH, &path);
            item->Release();
            if (FAILED(result)) return result;
            paths.emplace_back(path);
            CoTaskMemFree(path);
        }
        return paths.empty() ? E_INVALIDARG : S_OK;
    }

    bool CanExtract(IShellItemArray* selection)
    {
        if (!selection) return true;
        std::vector<std::wstring> paths;
        if (FAILED(SelectedPaths(selection, paths))) return false;
        for (const auto& path : paths)
        {
            if (!IsSupportedArchive(path)) return false;
        }
        return true;
    }

    bool CanOpen(IShellItemArray* selection)
    {
        if (!selection) return true;
        std::vector<std::wstring> paths;
        return SUCCEEDED(SelectedPaths(selection, paths))
            && paths.size() == 1 && IsSupportedArchive(paths[0]);
    }

    std::wstring ArchiveStem(IShellItemArray* selection)
    {
        std::vector<std::wstring> paths;
        if (FAILED(SelectedPaths(selection, paths)) || paths.size() != 1)
        {
            return IsKorean() ? L"\uc555\ucd95 \ud30c\uc77c" : L"Archive";
        }

        wchar_t value[MAX_PATH] = {};
        lstrcpynW(value, PathFindFileNameW(paths[0].c_str()), ARRAYSIZE(value));
        if ((GetFileAttributesW(paths[0].c_str()) & FILE_ATTRIBUTE_DIRECTORY) == 0)
        {
            PathRemoveExtensionW(value);
        }
        return value[0] == L'\0' ? (IsKorean() ? L"\uc555\ucd95 \ud30c\uc77c" : L"Archive") : value;
    }

    HRESULT Launch(CommandKind kind, IShellItemArray* selection)
    {
        std::vector<std::wstring> paths;
        RETURN_IF_FAILED(SelectedPaths(selection, paths));
        if (kind == CommandKind::SmartExtract || kind == CommandKind::ExtractHere || kind == CommandKind::Open)
        {
            for (const auto& path : paths)
            {
                if (!IsSupportedArchive(path)) return E_INVALIDARG;
            }
        }

        const std::wstring executable = ModuleDirectory() + L"\\sZIP.App.exe";
        const wchar_t* option = L"--compress";
        if (kind == CommandKind::SmartExtract) option = L"--extract-smart";
        else if (kind == CommandKind::ExtractHere) option = L"--extract-direct";
        else if (kind == CommandKind::Open) option = L"--open";
        else if (kind == CommandKind::CompressZip) option = L"--compress-zip";
        else if (kind == CommandKind::CompressSevenZip) option = L"--compress-7z";

        std::wstring commandLine = Quote(executable) + L" " + option;
        for (const auto& path : paths) commandLine += L" " + Quote(path);
        std::vector<wchar_t> buffer(commandLine.begin(), commandLine.end());
        buffer.push_back(L'\0');

        STARTUPINFOW startup = { sizeof(startup) };
        PROCESS_INFORMATION process = {};
        const std::wstring workingDirectory = ModuleDirectory();
        if (!CreateProcessW(executable.c_str(), buffer.data(), nullptr, nullptr, FALSE, 0,
                nullptr, workingDirectory.c_str(), &startup, &process))
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        CloseHandle(process.hThread);
        CloseHandle(process.hProcess);
        return S_OK;
    }

    class ExplorerCommand;

    class CommandEnumerator final : public IEnumExplorerCommand
    {
    public:
        CommandEnumerator() : _kinds
        {
            CommandKind::SmartExtract,
            CommandKind::ExtractHere,
            CommandKind::Open,
            CommandKind::CompressZip,
            CommandKind::CompressSevenZip,
            CommandKind::CompressDialog
        }
        {
            ++g_objectCount;
        }

        explicit CommandEnumerator(ULONG index) : CommandEnumerator() { _index = index; }
        ~CommandEnumerator() { --g_objectCount; }

        IFACEMETHODIMP QueryInterface(REFIID iid, void** object) override
        {
            if (!object) return E_POINTER;
            *object = nullptr;
            if (iid == IID_IUnknown || iid == __uuidof(IEnumExplorerCommand))
            {
                *object = static_cast<IEnumExplorerCommand*>(this);
                AddRef();
                return S_OK;
            }
            return E_NOINTERFACE;
        }

        IFACEMETHODIMP_(ULONG) AddRef() override { return InterlockedIncrement(&_references); }
        IFACEMETHODIMP_(ULONG) Release() override
        {
            const ULONG references = InterlockedDecrement(&_references);
            if (references == 0) delete this;
            return references;
        }

        IFACEMETHODIMP Next(ULONG count, IExplorerCommand** commands, ULONG* fetched) override;

        IFACEMETHODIMP Skip(ULONG count) override
        {
            _index = std::min(static_cast<ULONG>(_kinds.size()), _index + count);
            return _index < _kinds.size() ? S_OK : S_FALSE;
        }

        IFACEMETHODIMP Reset() override
        {
            _index = 0;
            return S_OK;
        }

        IFACEMETHODIMP Clone(IEnumExplorerCommand** result) override
        {
            if (!result) return E_POINTER;
            *result = new (std::nothrow) CommandEnumerator(_index);
            return *result ? S_OK : E_OUTOFMEMORY;
        }

    private:
        volatile long _references = 1;
        std::vector<CommandKind> _kinds;
        ULONG _index = 0;
    };

    class ExplorerCommand final : public IExplorerCommand
    {
    public:
        explicit ExplorerCommand(CommandKind kind) : _kind(kind) { ++g_objectCount; }
        ~ExplorerCommand() { --g_objectCount; }

        IFACEMETHODIMP QueryInterface(REFIID iid, void** object) override
        {
            if (!object) return E_POINTER;
            *object = nullptr;
            if (iid == IID_IUnknown || iid == __uuidof(IExplorerCommand))
            {
                *object = static_cast<IExplorerCommand*>(this);
                AddRef();
                return S_OK;
            }
            return E_NOINTERFACE;
        }

        IFACEMETHODIMP_(ULONG) AddRef() override { return InterlockedIncrement(&_references); }
        IFACEMETHODIMP_(ULONG) Release() override
        {
            const ULONG references = InterlockedDecrement(&_references);
            if (references == 0) delete this;
            return references;
        }

        IFACEMETHODIMP GetTitle(IShellItemArray* selection, PWSTR* title) override
        {
            if (_kind == CommandKind::Root)
                return CopyResult(CanExtract(selection) ? L"sZIP"
                    : (IsKorean() ? L"sZIP\uc73c\ub85c \uc555\ucd95\ud558\uae30" : L"Compress with sZIP"), title);
            if (_kind == CommandKind::SmartExtract)
                return CopyResult(IsKorean() ? L"\uc54c\uc544\uc11c \uc555\ucd95 \ud480\uae30" : L"Smart Extract", title);
            if (_kind == CommandKind::ExtractHere)
                return CopyResult(IsKorean() ? L"\uc5ec\uae30\uc5d0 \uc555\ucd95 \ud480\uae30" : L"Extract Here", title);
            if (_kind == CommandKind::Open)
                return CopyResult(IsKorean() ? L"sZIP\uc73c\ub85c \uc5f4\uae30" : L"Open with sZIP", title);
            if (_kind == CommandKind::CompressDialog)
                return CopyResult(IsKorean() ? L"\uc555\ucd95 \uc124\uc815..." : L"Compress with sZIP...", title);

            const std::wstring extension = _kind == CommandKind::CompressZip ? L".zip" : L".7z";
            const std::wstring fileName = ArchiveStem(selection) + extension;
            return CopyResult(IsKorean()
                ? L"\"" + fileName + L"\"\uc73c\ub85c \uc555\ucd95\ud558\uae30"
                : L"Compress to \"" + fileName + L"\"", title);
        }

        IFACEMETHODIMP GetIcon(IShellItemArray*, PWSTR* icon) override
        {
            return CopyResult(ModuleDirectory() + L"\\sZIP.App.exe,0", icon);
        }

        IFACEMETHODIMP GetToolTip(IShellItemArray*, PWSTR* tooltip) override
        {
            if (tooltip) *tooltip = nullptr;
            return E_NOTIMPL;
        }

        IFACEMETHODIMP GetCanonicalName(GUID* guid) override
        {
            if (!guid) return E_POINTER;
            *guid = _kind == CommandKind::Root ? CLSID_sZIPMenu : GUID_NULL;
            return S_OK;
        }

        IFACEMETHODIMP GetState(IShellItemArray* selection, BOOL, EXPCMDSTATE* state) override
        {
            if (!state) return E_POINTER;
            if (_kind == CommandKind::SmartExtract || _kind == CommandKind::ExtractHere)
                *state = CanExtract(selection) ? ECS_ENABLED : ECS_HIDDEN;
            else if (_kind == CommandKind::Open)
                *state = CanOpen(selection) ? ECS_ENABLED : ECS_HIDDEN;
            else
                *state = ECS_ENABLED;
            return S_OK;
        }

        IFACEMETHODIMP Invoke(IShellItemArray* selection, IBindCtx*) override
        {
            return _kind == CommandKind::Root ? E_NOTIMPL : Launch(_kind, selection);
        }

        IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override
        {
            if (!flags) return E_POINTER;
            *flags = _kind == CommandKind::Root ? ECF_HASSUBCOMMANDS : ECF_DEFAULT;
            return S_OK;
        }

        IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** commands) override
        {
            if (!commands) return E_POINTER;
            *commands = nullptr;
            if (_kind != CommandKind::Root) return E_NOTIMPL;
            *commands = new (std::nothrow) CommandEnumerator();
            return *commands ? S_OK : E_OUTOFMEMORY;
        }

    private:
        volatile long _references = 1;
        CommandKind _kind;
    };

    HRESULT CommandEnumerator::Next(ULONG count, IExplorerCommand** commands, ULONG* fetched)
    {
        if (!commands || (count != 1 && !fetched)) return E_POINTER;
        ULONG produced = 0;
        while (produced < count && _index < _kinds.size())
        {
            commands[produced] = new (std::nothrow) ExplorerCommand(_kinds[_index]);
            if (!commands[produced])
            {
                if (fetched) *fetched = produced;
                return E_OUTOFMEMORY;
            }
            ++produced;
            ++_index;
        }
        if (fetched) *fetched = produced;
        return produced == count ? S_OK : S_FALSE;
    }

    class ClassFactory final : public IClassFactory
    {
    public:
        explicit ClassFactory(CommandKind kind) : _kind(kind) { ++g_objectCount; }
        ~ClassFactory() { --g_objectCount; }

        IFACEMETHODIMP QueryInterface(REFIID iid, void** object) override
        {
            if (!object) return E_POINTER;
            *object = nullptr;
            if (iid == IID_IUnknown || iid == IID_IClassFactory)
            {
                *object = static_cast<IClassFactory*>(this);
                AddRef();
                return S_OK;
            }
            return E_NOINTERFACE;
        }

        IFACEMETHODIMP_(ULONG) AddRef() override { return InterlockedIncrement(&_references); }
        IFACEMETHODIMP_(ULONG) Release() override
        {
            const ULONG references = InterlockedDecrement(&_references);
            if (references == 0) delete this;
            return references;
        }

        IFACEMETHODIMP CreateInstance(IUnknown* outer, REFIID iid, void** object) override
        {
            if (outer) return CLASS_E_NOAGGREGATION;
            auto* command = new (std::nothrow) ExplorerCommand(_kind);
            if (!command) return E_OUTOFMEMORY;
            const HRESULT result = command->QueryInterface(iid, object);
            command->Release();
            return result;
        }

        IFACEMETHODIMP LockServer(BOOL lock) override
        {
            lock ? ++g_objectCount : --g_objectCount;
            return S_OK;
        }

    private:
        volatile long _references = 1;
        CommandKind _kind;
    };
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_module = module;
        DisableThreadLibraryCalls(module);
    }
    return TRUE;
}

extern "C" HRESULT __stdcall DllGetClassObject(
    REFCLSID classId, REFIID iid, void** object)
{
    CommandKind kind;
    if (classId == CLSID_sZIPMenu) kind = CommandKind::Root;
    else if (classId == CLSID_LegacyExtractDirect) kind = CommandKind::ExtractHere;
    else if (classId == CLSID_LegacyExtractSmart) kind = CommandKind::SmartExtract;
    else return CLASS_E_CLASSNOTAVAILABLE;

    auto* factory = new (std::nothrow) ClassFactory(kind);
    if (!factory) return E_OUTOFMEMORY;
    const HRESULT result = factory->QueryInterface(iid, object);
    factory->Release();
    return result;
}

extern "C" HRESULT __stdcall DllCanUnloadNow()
{
    return g_objectCount == 0 ? S_OK : S_FALSE;
}
