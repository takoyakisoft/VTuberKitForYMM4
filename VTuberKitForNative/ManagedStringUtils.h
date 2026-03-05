#pragma once

#include <Windows.h>
#include <vcclr.h>
#include <string>
#include <vector>

namespace VTuberKitForNative {

inline std::string ManagedStringToUtf8(System::String^ value)
{
    if (value == nullptr || value->Length == 0)
    {
        return std::string();
    }

    pin_ptr<const wchar_t> wideChars = PtrToStringChars(value);
    const int utf8Size = WideCharToMultiByte(CP_UTF8, 0, wideChars, value->Length, nullptr, 0, nullptr, nullptr);
    if (utf8Size <= 0)
    {
        return std::string();
    }

    std::string utf8(utf8Size, '\0');
    WideCharToMultiByte(CP_UTF8, 0, wideChars, value->Length, &utf8[0], utf8Size, nullptr, nullptr);
    return utf8;
}

inline System::String^ Utf8ToManagedString(const char* value)
{
    if (value == nullptr || *value == '\0')
    {
        return gcnew System::String("");
    }

    const int wideSize = MultiByteToWideChar(CP_UTF8, 0, value, -1, nullptr, 0);
    if (wideSize <= 0)
    {
        return gcnew System::String(value);
    }

    std::vector<wchar_t> wideBuffer(wideSize);
    MultiByteToWideChar(CP_UTF8, 0, value, -1, wideBuffer.data(), wideSize);
    return gcnew System::String(wideBuffer.data());
}

} // namespace VTuberKitForNative
