#include "Live2DWrapper.h"
#include "Live2DPal.h"
#include <CubismFramework.hpp>

using namespace Csm;
using namespace System;

namespace VTuberKitForNative {

bool Live2DPlugin::InitializePlugin() {
    Live2DManager^ manager = Live2DManager::GetInstance();
    if (!manager->Initialize()) {
        Live2DPal::PrintLogLn("Failed to initialize Live2DManager");
        return false;
    }
    
    return true;
}

void Live2DPlugin::ReleasePlugin() {
    Live2DManager::ReleaseInstance();
}

String^ Live2DPlugin::GetVersion() {
    return gcnew String("2.1.0.0");
}

String^ Live2DPlugin::GetCubismVersion() {
    return gcnew String("5.0.0");
}

} // namespace VTuberKitForNative
