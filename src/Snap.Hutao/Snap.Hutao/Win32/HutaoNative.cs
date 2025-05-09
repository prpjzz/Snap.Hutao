// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.Core.ExceptionService;
using Snap.Hutao.Win32.Foundation;
using System.Runtime.InteropServices;
using WinRT;
using WinRT.Interop;

namespace Snap.Hutao.Win32;

[Guid("d00f73ff-a1c7-4091-8cb6-d90991dd40cb")]
internal sealed unsafe class HutaoNative
{
    private readonly ObjectReference<Vftbl> objRef;

    public HutaoNative(ObjectReference<Vftbl> objRef)
    {
        this.objRef = objRef;
    }

    [field: MaybeNull]
    public static HutaoNative Instance
    {
        get => LazyInitializer.EnsureInitialized(ref field, static () =>
        {
#if DEBUG
            HutaoNativeMethods.HutaoInitializeWilCallbacks();
#endif
            return HutaoNativeMethods.HutaoCreateInstance();
        });
    }

    public HutaoNativeLoopbackSupport MakeLoopbackSupport()
    {
        HutaoException.NotSupportedIf(objRef is null, "IHutaoNative.MakeLoopbackSupport is not supported");
        nint pv = default;
        Marshal.ThrowExceptionForHR(objRef.Vftbl.MakeLoopbackSupport(objRef.ThisPtr, (HutaoNativeLoopbackSupport.Vftbl**)&pv));
        return new(ObjectReference<HutaoNativeLoopbackSupport.Vftbl>.Attach(ref pv, typeof(HutaoNativeLoopbackSupport).GUID));
    }

    public HutaoNativeRegistryNotification MakeRegistryNotification(ReadOnlySpan<char> keyPath)
    {
        HutaoException.NotSupportedIf(objRef is null, "IHutaoNative.MakeRegistryNotification is not supported");
        fixed (char* keyPathPtr = keyPath)
        {
            nint pv = default;
            Marshal.ThrowExceptionForHR(objRef.Vftbl.MakeRegistryNotification(objRef.ThisPtr, keyPathPtr, (HutaoNativeRegistryNotification.Vftbl**)&pv));
            return new(ObjectReference<HutaoNativeRegistryNotification.Vftbl>.Attach(ref pv, typeof(HutaoNativeRegistryNotification).GUID));
        }
    }

    public HutaoNativeWindowSubclass MakeWindowSubclass(HWND hWnd, HutaoNativeWindowSubclassCallback callback, nint userData)
    {
        HutaoException.NotSupportedIf(objRef is null, "IHutaoNative.MakeWindowSubclass is not supported");
        nint pv = default;
        Marshal.ThrowExceptionForHR(objRef.Vftbl.MakeWindowSubclass(objRef.ThisPtr, hWnd, callback, userData, (HutaoNativeWindowSubclass.Vftbl**)&pv));
        return new(ObjectReference<HutaoNativeWindowSubclass.Vftbl>.Attach(ref pv, typeof(HutaoNativeWindowSubclass).GUID));
    }

    internal readonly struct Vftbl
    {
#pragma warning disable CS0649
        internal readonly IUnknownVftbl IUnknownVftbl;
        internal readonly delegate* unmanaged[Stdcall]<nint, HutaoNativeLoopbackSupport.Vftbl**, HRESULT> MakeLoopbackSupport;
        internal readonly delegate* unmanaged[Stdcall]<nint, PCWSTR, HutaoNativeRegistryNotification.Vftbl**, HRESULT> MakeRegistryNotification;
        internal readonly delegate* unmanaged[Stdcall]<nint, HWND, HutaoNativeWindowSubclassCallback, nint, HutaoNativeWindowSubclass.Vftbl**, HRESULT> MakeWindowSubclass;
#pragma warning restore CS0649
    }
}