#include <windows.h>
#include <shobjidl.h>
#include <shlwapi.h>
#include <strsafe.h>
#include <atomic>
#include <cwctype>
#include <new>
#include <string>
#include <vector>

#pragma comment(lib, "ole32.lib")
#pragma comment(lib, "shell32.lib")
#pragma comment(lib, "shlwapi.lib")

#define RETURN_IF_FAILED(expression) do { const HRESULT result = (expression); if (FAILED(result)) return result; } while (false)

namespace
{
    const CLSID CLSID_Compress =
        {0x5fd30db9, 0xb45b, 0x48dd, {0xa3, 0x59, 0x30, 0xbe, 0xea, 0x4c, 0xa4, 0xe1}};
    const CLSID CLSID_ExtractDirect =
        {0xf4da9d54, 0x7593, 0x4d8a, {0xa8, 0x58, 0xa8, 0xf2, 0x70, 0x22, 0xfe, 0xd2}};
    const CLSID CLSID_ExtractSmart =
        {0xc98ac2e7, 0xa656, 0x472e, {0xbf, 0x79, 0x3f, 0xdd, 0xa6, 0xa0, 0x62, 0x1e}};

    enum class CommandKind { Compress, ExtractDirect, ExtractSmart };
    std::atomic<long> g_objectCount = 0;
    HMODULE g_module = nullptr;

    HRESULT CopyResult(const std::wstring& value, PWSTR* result)
    {
        if (!result) return E_POINTER;
        return SHStrDupW(value.c_str(), result);
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

    HRESULT Launch(CommandKind kind, IShellItemArray* selection)
    {
        std::vector<std::wstring> paths;
        RETURN_IF_FAILED(SelectedPaths(selection, paths));
        if (kind != CommandKind::Compress)
        {
            for (const auto& path : paths)
            {
                if (!IsSupportedArchive(path)) return E_INVALIDARG;
            }
        }

        const std::wstring executable = ModuleDirectory() + L"\\sZIP.App.exe";
        const wchar_t* option = kind == CommandKind::Compress
            ? L"--compress"
            : kind == CommandKind::ExtractDirect ? L"--extract-direct" : L"--extract-smart";
        std::wstring commandLine = Quote(executable) + L" " + option;
        for (const auto& path : paths) commandLine += L" " + Quote(path);
        std::vector<wchar_t> buffer(commandLine.begin(), commandLine.end());
        buffer.push_back(L'\0');

        STARTUPINFOW startup = { sizeof(startup) };
        PROCESS_INFORMATION process = {};
        if (!CreateProcessW(executable.c_str(), buffer.data(), nullptr, nullptr, FALSE, 0,
                nullptr, ModuleDirectory().c_str(), &startup, &process))
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        CloseHandle(process.hThread);
        CloseHandle(process.hProcess);
        return S_OK;
    }

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

        IFACEMETHODIMP GetTitle(IShellItemArray*, PWSTR* title) override
        {
            if (_kind == CommandKind::Compress) return CopyResult(L"Compress with sZIP", title);
            if (_kind == CommandKind::ExtractDirect) return CopyResult(L"sZIP Extract", title);
            return CopyResult(L"sZIP Smart Extract", title);
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
            *guid = _kind == CommandKind::Compress
                ? CLSID_Compress
                : _kind == CommandKind::ExtractDirect ? CLSID_ExtractDirect : CLSID_ExtractSmart;
            return S_OK;
        }

        IFACEMETHODIMP GetState(IShellItemArray* selection, BOOL, EXPCMDSTATE* state) override
        {
            if (!state) return E_POINTER;
            *state = _kind == CommandKind::Compress || CanExtract(selection)
                ? ECS_ENABLED : ECS_HIDDEN;
            return S_OK;
        }

        IFACEMETHODIMP Invoke(IShellItemArray* selection, IBindCtx*) override
        {
            return Launch(_kind, selection);
        }

        IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override
        {
            if (!flags) return E_POINTER;
            *flags = ECF_DEFAULT;
            return S_OK;
        }

        IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** commands) override
        {
            if (commands) *commands = nullptr;
            return E_NOTIMPL;
        }

    private:
        volatile long _references = 1;
        CommandKind _kind;
    };

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
    if (classId == CLSID_Compress) kind = CommandKind::Compress;
    else if (classId == CLSID_ExtractDirect) kind = CommandKind::ExtractDirect;
    else if (classId == CLSID_ExtractSmart) kind = CommandKind::ExtractSmart;
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
