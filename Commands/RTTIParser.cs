using System.Diagnostics.Contracts;
using System.Text;
using System.Threading.Tasks;
using RTTIScanner.ClassExtensions;

namespace RTTIScanner.Commands
{
    public class RTTIParser
    {
        public static async Task<string> ReadRemoteRuntimeTypeInformationAsync(IntPtr address)
        {
            if (address.MayBeValid())
            {
                string rtti = null;
                var objectLocatorPtr = await ReadRemoteIntPtrAsync(address - IntPtr.Size);
                if (objectLocatorPtr.MayBeValid())
                {
#if RTTISCANNER64
					rtti = await ReadRemoteRuntimeTypeInformation64Async(objectLocatorPtr);
#else
                    //rtti = ReadRemoteRuntimeTypeInformation32Async(objectLocatorPtr);
#endif

                }

                return rtti;
            }

            return null;
        }

        public static async Task<string> ReadRemoteRuntimeTypeInformation64Async(IntPtr address)
        {
            if (address.MayBeValid())
            {
                int baseOffset = await ReadRemoteInt32Async(address + 0x14);
                if (baseOffset != 0)
                {
                    var baseAddress = address - baseOffset;

                    var classHierarchyDescriptorOffset = await ReadRemoteInt32Async(address + 0x10);
                    if (classHierarchyDescriptorOffset != 0)
                    {
                        var classHierarchyDescriptorPtr = baseAddress + classHierarchyDescriptorOffset;

                        var baseClassCount = await ReadRemoteInt32Async(classHierarchyDescriptorPtr + 0x08);
                        if (baseClassCount > 0 && baseClassCount < 25)
                        {
                            var baseClassArrayOffset = await ReadRemoteInt32Async(classHierarchyDescriptorPtr + 0x0C);
                            if (baseClassArrayOffset != 0)
                            {
                                var baseClassArrayPtr = baseAddress + baseClassArrayOffset;

                                var sb = new StringBuilder();
                                for (var i = 0; i < baseClassCount; ++i)
                                {
                                    var baseClassDescriptorOffset = await ReadRemoteInt32Async(baseClassArrayPtr + (4 * i));
                                    if (baseClassDescriptorOffset != 0)
                                    {
                                        var baseClassDescriptorPtr = baseAddress + baseClassDescriptorOffset;

                                        var typeDescriptorOffset = await ReadRemoteInt32Async(baseClassDescriptorPtr);
                                        if (typeDescriptorOffset != 0)
                                        {
                                            var typeDescriptorPtr = baseAddress + typeDescriptorOffset;

                                            var name = await ReadRemoteStringUntilFirstNullCharacterAsync(typeDescriptorPtr + 0x14, Encoding.UTF8, 60);
                                            if (string.IsNullOrEmpty(name))
                                            {
                                                break;
                                            }

                                            if (name.EndsWith("@@"))
                                            {
                                                name = UndecorateSymbolName("?" + name);
                                            }

                                            sb.Append(name);
                                            sb.Append(" : ");

                                            continue;
                                        }
                                    }

                                    break;
                                }

                                if (sb.Length != 0)
                                {
                                    sb.Length -= 3;

                                    return sb.ToString();
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }

        public static async Task<IntPtr> ReadRemoteIntPtrAsync(IntPtr address)
        {
#if RTTISCANNER64
            return (IntPtr) await ReadRemoteInt64Async(address);
#else
            return (IntPtr) await ReadRemoteInt32Async(address);
#endif
        }

        public static async Task<long> ReadRemoteInt64Async(IntPtr address)
        {
            var data = await RemoteProcess.Instance.ReadRemoteMemoryAsync(address, sizeof(long));

            return BitConverter.ToInt64(data, 0);
        }

        public static async Task<int> ReadRemoteInt32Async(IntPtr address)
        {
            var data = await RemoteProcess.Instance.ReadRemoteMemoryAsync(address, sizeof(int));

            return BitConverter.ToInt32(data, 0);
        }

        public static async Task<string> ReadRemoteStringUntilFirstNullCharacterAsync(IntPtr address, Encoding encoding, int length)
        {
            Contract.Requires(encoding != null);
            Contract.Requires(length >= 0);
            Contract.Ensures(Contract.Result<string>() != null);

            var data = await RemoteProcess.Instance.ReadRemoteMemoryAsync(address, length * encoding.GuessByteCountPerChar());

            // TODO We should cache the pattern per encoding.
            var index = PatternScanner.FindPattern(BytePattern.From(new byte[encoding.GuessByteCountPerChar()]), data);
            if (index == -1)
            {
                index = data.Length;
            }

            try
            {
                return encoding.GetString(data, 0, Math.Min(index, data.Length));
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string UndecorateSymbolName(string name)
        {
            var sb = new StringBuilder(255);
            if (NativeAPI.UnDecorateSymbolName(name, sb, sb.Capacity, /*UNDNAME_NAME_ONLY*/0x1000) != 0)
            {
                return sb.ToString();
            }
            return name;
        }
    }
}
