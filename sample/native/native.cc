#include <napi.h>
// #include <shobjidl_core.h>
#include <windows.h>

#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.UI.Notifications.h>
#include <winrt/Windows.Data.Xml.Dom.h>

#include <MddBootstrap.h>
#include <winrt/Microsoft.Windows.AI.h>
#include <winrt/Microsoft.Windows.AI.Text.h>

#include <winrt/Windows.AI.Actions.h>
#include <combaseapi.h>

#include <codecvt>

using namespace winrt;
using namespace winrt::Windows::UI::Notifications;
using namespace winrt::Windows::Data::Xml::Dom;
using namespace winrt::Windows::ApplicationModel;

using namespace Microsoft::Windows::AI;
using namespace Microsoft::Windows::AI::Text;

using namespace Windows::AI::Actions;

std::once_flag winrt_init_flag;

// Helper function to create ActionRuntime using COM activation
Windows::AI::Actions::ActionRuntime CreateActionRuntime() {
    try {
        // NOTE: This Guid is subject to change in the future
        GUID actionRuntimeClsid = { 0xC36FEF7E, 0x35F3, 0x4192, { 0x9F, 0x2C, 0xAF, 0x1F, 0xD4, 0x25, 0xFB, 0x85 } };
        GUID iActionRuntimeIID = { 0x206EFA2C, 0xC909, 0x508A, { 0xB4, 0xB0, 0x94, 0x82, 0xBE, 0x96, 0xDB, 0x9C } };
        
        void* pActionRuntime = nullptr;
        HRESULT hr = CoCreateInstance(
            actionRuntimeClsid,
            nullptr,
            CLSCTX_LOCAL_SERVER,
            iActionRuntimeIID,
            &pActionRuntime
        );
        
        if (FAILED(hr)) {
            throw winrt::hresult_error(hr, L"Failed to create ActionRuntime");
        }
        
        // Convert the raw pointer to WinRT object
        return winrt::Windows::AI::Actions::ActionRuntime{ pActionRuntime, winrt::take_ownership_from_abi };
    }
    catch (...) {
        throw;
    }
}

// void EnsureWinRTInitialized() {
//     std::call_once(winrt_init_flag, []() {
//         winrt::init_apartment(winrt::apartment_type::multi_threaded);
//     });
// }

// Function to display a Windows notification
void ShowNotification(const Napi::CallbackInfo& info) {
    Napi::Env env = info.Env();

    try {
        // Get arguments from JavaScript (title and message)
        std::string title = info[0].As<Napi::String>();
        std::string message = info[1].As<Napi::String>();

        // Initialize WinRT
        // EnsureWinRTInitialized();

        // Define notification XML
        std::wstring xml = L"<toast><visual><binding template='ToastGeneric'><text>";
        xml += std::wstring(title.begin(), title.end());
        xml += L"</text><text>";
        xml += std::wstring(message.begin(), message.end());
        xml += L"</text></binding></visual></toast>";

        // Create a ToastNotificationManager
        ToastNotifier notifier = ToastNotificationManager::CreateToastNotifier();

        // Parse the XML
        XmlDocument toastXml;
        toastXml.LoadXml(xml);
        
        // Create a toast notification
        ToastNotification toast{ toastXml };
        notifier.Show(toast);
    } catch (const winrt::hresult_error& ex) {
        Napi::Error::New(env, winrt::to_string(ex.message())).ThrowAsJavaScriptException();
    } catch (const std::exception& ex) {
        // Handle exceptions and throw back to JavaScript
        Napi::Error::New(env, ex.what()).ThrowAsJavaScriptException();
    } catch (...) {
        Napi::Error::New(env, "Unknown error occurred").ThrowAsJavaScriptException();
    }
}


Napi::String CallPhiSilica(const Napi::CallbackInfo& info) {
    // ::RoInitialize(RO_INIT_SINGLETHREADED);

    Napi::Env env = info.Env();

    if (info.Length() < 1 || !info[0].IsString()) {
        Napi::TypeError::New(env, "String arguments expected").ThrowAsJavaScriptException();
        return Napi::String::New(env, "error");
    }

    std::string prompt = info[0].As<Napi::String>();
    Napi::Function callback = info[1].As<Napi::Function>();

    auto tsfn = Napi::ThreadSafeFunction::New(
        env,
        callback, // JavaScript callback
        "StreamingCallback", // Resource name
        0, // Unlimited queue
        1 // Only one thread will use this
    );

    // Convert std::string to std::wstring
    std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>> converter;
    std::wstring widePrompt = converter.from_bytes(prompt);

    // PACKAGE_VERSION minVersion{};
    // minVersion.Major = Microsoft::WindowsAppSDK::Runtime::Version::Major;
    // minVersion.Minor = Microsoft::WindowsAppSDK::Runtime::Version::Minor;
    // minVersion.Revision = Microsoft::WindowsAppSDK::Runtime::Version::Revision;
    // minVersion.Build = Microsoft::WindowsAppSDK::Runtime::Version::Build;

    // if (FAILED(MddBootstrapInitialize(
    //     Microsoft::WindowsAppSDK::Release::MajorMinor,
    //     Microsoft::WindowsAppSDK::Release::VersionTag,
    //     minVersion))) {
        
    //         Napi::TypeError::New(env, "Can't initialize WASDK").ThrowAsJavaScriptException();
    //         return Napi::String::New(env, "error");

    // }

    std::thread([prompt, tsfn, widePrompt]() {
        // Check to see if the langauge model is available on this machine. If it's not, the workflow
        // system can download it on demand. The very first download of the model takes more time than
        // subsequent downloads.
        //
        // An interactive app could show a progress dialog while MakeAvailableAsync() is running. This
        // sample intentionally blocks until the model is available.
        try
        {
            auto const limitedAccessFeatureToken = /* contact CSS for these values */;
            auto const limitedAccessFeatureAttestation = /* contact CSS for these values */;

            auto access = LimitedAccessFeatures::TryUnlockFeature(
                L"com.microsoft.windows.ai.languagemodel",
                limitedAccessFeatureToken,
                limitedAccessFeatureAttestation);

            auto status = access.Status();

            if ((status == LimitedAccessFeatureStatus::Available) ||
                (status == LimitedAccessFeatureStatus::AvailableWithoutToken))
            {
                // winrt::init_apartment(winrt::apartment_type::single_threaded);
                if (LanguageModel::GetReadyState() == AIFeatureReadyState::NotReady)
                {
                    auto op = LanguageModel::EnsureReadyAsync().get();
                }

                if (LanguageModel::GetReadyState() == AIFeatureReadyState::Ready)
                {
                    // Use ThreadSafeFunction to call back to JavaScript thread
                    tsfn.BlockingCall([](Napi::Env env, Napi::Function jsCallback) {
                        jsCallback.Call({ Napi::String::New(env, "ready") });
                    });

                    auto languageModel = LanguageModel::CreateAsync().get();
                    // auto options = LanguageModelOptions();
                    // options.TopK(15);
                    // options.Temperature(0.9f);

                    //auto responseWait = languageModel.GenerateResponseAsync(widePrompt, options);
                    auto textSummarizer = winrt::Microsoft::Windows::AI::Text::TextSummarizer(languageModel);
                    auto responseWait = textSummarizer.SummarizeAsync(widePrompt);
                    responseWait.Progress([tsfn](auto const& sender, auto const& progress) {
                        std::string responseString = winrt::to_string(progress.c_str());

                        tsfn.BlockingCall([responseString](Napi::Env env, Napi::Function jsCallback) {
                            jsCallback.Call({ Napi::String::New(env, responseString) });
                        });
                    });

                    auto response = responseWait.get();
                    
                    // Send final response
                    std::string finalResponse = winrt::to_string(response.Text());
                    tsfn.BlockingCall([finalResponse](Napi::Env env, Napi::Function jsCallback) {
                        jsCallback.Call({ Napi::String::New(env, finalResponse) });
                    });
                }
                else
                {
                    // Use ThreadSafeFunction to call back to JavaScript thread
                    tsfn.BlockingCall([](Napi::Env env, Napi::Function jsCallback) {
                        jsCallback.Call({ Napi::String::New(env, "not ready") });
                    });
                }
            }
            else
            {
                std::string errorMsg = "Limited Access Feature not available. Status: " + std::to_string(static_cast<int>(status));
                
                // Use ThreadSafeFunction to call back to JavaScript thread with error
                tsfn.BlockingCall([errorMsg](Napi::Env env, Napi::Function jsCallback) {
                    jsCallback.Call({ Napi::String::New(env, "error"), Napi::String::New(env, errorMsg) });
                });
            }
        }
        catch (const winrt::hresult_error& e)
        {
            std::string errorMsg = "Failed to make language model available: " + winrt::to_string(e.message());
            
            // Use ThreadSafeFunction to call back to JavaScript thread with error
            tsfn.BlockingCall([errorMsg](Napi::Env env, Napi::Function jsCallback) {
                jsCallback.Call({ Napi::String::New(env, "error"), Napi::String::New(env, errorMsg) });
            });
        }
        
        // Clean up the ThreadSafeFunction
        tsfn.Release();
    }).detach();

    
    



    return Napi::String::New(env, "started");
}

void SetActionAvailability(const Napi::CallbackInfo& info) {
    Napi::Env env = info.Env();

    if (info.Length() != 2 || !info[0].IsString() || !info[1].IsBoolean()) {
        Napi::TypeError::New(env, "String and boolean arguments expected").ThrowAsJavaScriptException();
        return;
    }

    bool isAvailable = info[1].As<Napi::Boolean>();
    std::string actionName = info[0].As<Napi::String>();

    try
    {
        auto actionRuntime = CreateActionRuntime();
        
        // Convert std::string to std::wstring for the API
        std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>> converter;
        std::wstring wideActionName = converter.from_bytes(actionName);
        
        actionRuntime.SetActionAvailability(wideActionName, isAvailable);
        actionRuntime.Close();
    }
    catch (const winrt::hresult_error& e)
    {
        std::string errorMsg = "Failed to set action availability: " + winrt::to_string(e.message());
        Napi::Error::New(env, errorMsg).ThrowAsJavaScriptException();
    }
    catch (const std::exception& e)
    {
        Napi::Error::New(env, e.what()).ThrowAsJavaScriptException();
    }
}

// Initialize the module
Napi::Object Init(Napi::Env env, Napi::Object exports) {
    exports.Set(Napi::String::New(env, "showNotification"), Napi::Function::New(env, ShowNotification));
    exports.Set(Napi::String::New(env, "callPhiSilica"), Napi::Function::New(env, CallPhiSilica));
    exports.Set(Napi::String::New(env, "setActionAvailability"), Napi::Function::New(env, SetActionAvailability));
    return exports;
}

NODE_API_MODULE(addon, Init)