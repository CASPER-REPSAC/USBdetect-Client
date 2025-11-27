using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace USBdetect // 네임스페이스는 프로젝트에 맞게 수정
{
    // C++ 구조체와 1:1 매핑
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct USB_DEVICE_INFO
    {
        public uint DeviceIndex;            // 장치 인덱스
        public ushort VendorId;             // VID
        public ushort ProductId;            // PID

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string SerialNumber;         // 시리얼 번호

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string ProductString;        // 제품명

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string ManufacturerString;   // 제조사

        [MarshalAs(UnmanagedType.U1)]       // 1바이트 Boolean 처리
        public bool IsBlocked;              // 차단 여부

        public override string ToString()
        {
            return $"Index: {DeviceIndex}, VID: {VendorId}, PID: {ProductId}, Serial: {SerialNumber}, Product: {ProductString}, Manufacturer: {ManufacturerString}, Blocked: {IsBlocked}";
        }
    }

    public static class UsbDriverProxy
    {
        // DLL 파일명은 변경하지 마세요.
        private const string DllName = "UsbMock.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void InitUsbMock();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void GetRandomUsbInfo(ref USB_DEVICE_INFO pInfo, uint index);
    }

    public static class UsbStorageService
    {
        public static List<USB_DEVICE_INFO> GetAllUsbDevices(int count)
        {
            var list = new List<USB_DEVICE_INFO>();
            for (uint i = 0; i < count; i++)
            {
                var info = new USB_DEVICE_INFO();
                UsbDriverProxy.GetRandomUsbInfo(ref info, i);
                Console.WriteLine(info.ToString()); // USB 정보 콘솔 출력
                list.Add(info);
            }
            return list;
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