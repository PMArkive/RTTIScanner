using RTTIScanner.ClassExtensions;
using System.Diagnostics.Contracts;
using System.Text;

namespace RTTIScanner.Implement
{
    public class RTTIParser
    {
        // Reference: https://github.com/ReClassNET/ReClass.NET/blob/a02fcb9bd669c8f81facd3ee9ad57cdcbf2cc0e1/ReClass.NET/Memory/RemoteProcess.cs#L190
        public static string ReadRemoteRuntimeTypeInformation(IntPtr address)
        {
            if (address.MayBeValid())
            {
                try
                {
                    string rtti = null;
                    var objectLocatorPtr = ReadRemoteIntPtr(address - IntPtr.Size);
                    if (objectLocatorPtr.MayBeValid())
                    {
#if RTTISCANNER64
                        rtti = ReadRemoteRuntimeTypeInformation64(objectLocatorPtr);
#else
                        //rtti = ReadRemoteRuntimeTypeInformation32(objectLocatorPtr);
#endif

                    }

                    return rtti;
                }
                catch (Exception ex)
                {
                    RTTIScannerImpl.ErrorResult($"Catched error reading process memory: {ex.Message}");
                    return null;
                }
            }

            return null;
        }

        public static string ReadRemoteRuntimeTypeInformation64(IntPtr address)
        {
            try
            {
                if (address.MayBeValid())
                {
                    int baseOffset = ReadRemoteInt32(address + 0x14);
                    if (baseOffset != 0)
                    {
                        var baseAddress = address - baseOffset;

                        var classHierarchyDescriptorOffset = ReadRemoteInt32(address + 0x10);
                        if (classHierarchyDescriptorOffset != 0)
                        {
                            var classHierarchyDescriptorPtr = baseAddress + classHierarchyDescriptorOffset;

                            var baseClassCount = ReadRemoteInt32(classHierarchyDescriptorPtr + 0x08);
                            if (baseClassCount > 0 && baseClassCount < 25)
                            {
                                var baseClassArrayOffset = ReadRemoteInt32(classHierarchyDescriptorPtr + 0x0C);
                                if (baseClassArrayOffset != 0)
                                {
                                    var baseClassArrayPtr = baseAddress + baseClassArrayOffset;

                                    var sb = new StringBuilder();
                                    for (var i = 0; i < baseClassCount; ++i)
                                    {
                                        var baseClassDescriptorOffset = ReadRemoteInt32(baseClassArrayPtr + (4 * i));
                                        if (baseClassDescriptorOffset != 0)
                                        {
                                            var baseClassDescriptorPtr = baseAddress + baseClassDescriptorOffset;

                                            var typeDescriptorOffset = ReadRemoteInt32(baseClassDescriptorPtr);
                                            if (typeDescriptorOffset != 0)
                                            {
                                                var typeDescriptorPtr = baseAddress + typeDescriptorOffset;

                                                var name = ReadRemoteStringUntilFirstNullCharacter(typeDescriptorPtr + 0x14, Encoding.UTF8, 60);
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
            }
            catch (Exception ex)
            {
                RTTIScannerImpl.ErrorResult($"Catched error reading process memory: {ex.Message}");
                return null;
            }

            return null;
        }

        public static IntPtr ReadRemoteIntPtr(IntPtr address)
        {
            try
            {
#if RTTISCANNER64
                return (IntPtr)ReadRemoteInt64(address);
#else
                return (IntPtr)ReadRemoteInt32(address);
#endif
            }
            catch (Exception ex)
            {
                RTTIScannerImpl.ErrorResult($"Catched error reading process memory: {ex.Message}");
                return IntPtr.Zero;
            }
        }

        public static long ReadRemoteInt64(IntPtr address)
        {
            try
            {
                var data = RemoteProcess.Instance.ReadRemoteMemory(address, sizeof(long));

                return BitConverter.ToInt64(data, 0);
            }
            catch (Exception ex)
            {
                RTTIScannerImpl.ErrorResult($"Catched error reading process memory: {ex.Message}");
                return 0;
            }
        }

        public static int ReadRemoteInt32(IntPtr address)
        {
            try
            {
                var data = RemoteProcess.Instance.ReadRemoteMemory(address, sizeof(int));

                return BitConverter.ToInt32(data, 0);
            }
            catch (Exception ex)
            {
                RTTIScannerImpl.ErrorResult($"Catched error reading process memory: {ex.Message}");
                return 0;
            }
        }

        public static string ReadRemoteStringUntilFirstNullCharacter(IntPtr address, Encoding encoding, int length)
        {
            Contract.Requires(encoding != null);
            Contract.Requires(length >= 0);
            Contract.Ensures(Contract.Result<string>() != null);

            var data = RemoteProcess.Instance.ReadRemoteMemory(address, length * encoding.GuessByteCountPerChar());

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
