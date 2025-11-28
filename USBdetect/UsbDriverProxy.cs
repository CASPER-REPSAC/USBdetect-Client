using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace USBdetect // 네임스페이스는 프로젝트에 맞게 수정
{
    // 새 DLL의 정보 구조체에 맞춰 필드 구성 (wchar_t 고정 길이 배열 매핑)
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct USB_DEVICE_INFO
    {
        public uint DeviceIndex;            // 장치 인덱스
        public ushort VendorId;             // VID
        public ushort ProductId;            // PID

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string HardwareId;           // 하드웨어 ID

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string FriendlyName;         // 표시 이름

        [MarshalAs(UnmanagedType.U1)]
        public bool IsWhitelisted;          // 화이트리스트 여부

        public override string ToString()
        {
            return $"Index: {DeviceIndex}, VID: {VendorId}, PID: {ProductId}, HardwareId: {HardwareId}, Name: {FriendlyName}, Whitelisted: {IsWhitelisted}";
        }
    }

    public static class UsbDriverProxy
    {
        // 새 DLL 파일명으로 변경
        private const string DllName = "UsbBlockerSDK.dll";

        // 내보낸 함수 시그니처에 맞는 P/Invoke 선언 (일반적인 패턴)
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint GetUsbDeviceCount();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetUsbDeviceInfo(uint index, ref USB_DEVICE_INFO pInfo); // 0=성공, <0=에러

        public static List<USB_DEVICE_INFO> GetAllFromDll()
        {
            var result = new List<USB_DEVICE_INFO>();
            uint count = 0;
            try { count = GetUsbDeviceCount(); }
            catch { return result; }
            for (uint i = 0; i < count; i++)
            {
                var info = new USB_DEVICE_INFO();
                int rc = -1;
                try { rc = GetUsbDeviceInfo(i, ref info); }
                catch { rc = -1; }
                if (rc == 0)
                {
                    result.Add(info);
                    Console.WriteLine(info.ToString());
                }
            }
            return result;
        }
    }

    public static class UsbStorageService
    {
        public static List<USB_DEVICE_INFO> GetAllUsbDevices(int count)
        {
            // 새 DLL에서 전체 목록을 가져와 요청된 개수로 트림
            var devices = UsbDriverProxy.GetAllFromDll();
            if (devices.Count > count)
            {
                devices = devices.GetRange(0, count);
            }
            return devices;
        }
    }

    public class UsbDeviceListFormatter
    {
        public static string ConvertToJson(List<USB_DEVICE_INFO> devices)
        {
            var deviceListJson = JsonSerializer.Serialize(new
            {
                type = "deviceList",
                data = devices
            }, new JsonSerializerOptions { IncludeFields = true });

            return deviceListJson;
        }
    }
}