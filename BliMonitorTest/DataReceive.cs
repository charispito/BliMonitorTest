using BliMonitorTest.util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BliMonitorTest
{
    public partial class OneChannelWindow
    {        
        private void receiveData(byte[] data, int Length)
        {
            if (channel.IsNewVersion)
            {
                if (Length > 0)
                {
                    byte[] buffer = data.Slice(Length);
                    //buffer.PrintHex(1);
                    if (parameterCnt > 0)
                    {
                        parameterReceived.AddRange(buffer);

                        if (parameterReceived.Count >= 70)
                        {
                            byte[] receive = parameterReceived.ToArray();
                            int s_idx = getNewStxIndex(receive);
                            if (s_idx == 0)
                            {
                                if (receive.Length > 70)
                                {
                                    byte[] cmd = receive.Slice(70);
                                    byte[] etc = receive.Slice(70, parameterReceived.Count - 70);
                                    parameterReceived.Clear();
                                    parameterReceived.AddRange(etc);
                                    CheckCommand(cmd);
                                }
                                else
                                {
                                    parameterReceived.Clear();
                                    CheckCommand(receive);
                                }
                            }
                            else
                            {
                                if (receive[s_idx - 1] == 0x34)
                                {
                                    byte[] command = receive.Slice(s_idx);
                                    byte[] etc = receive.Slice(s_idx, receive.Length - s_idx);
                                    if (command.Length == 70)
                                    {
                                        parameterReceived.Clear();
                                        parameterReceived.AddRange(etc);
                                    }
                                    else
                                    {
                                        parameterReceived.Clear();
                                        parameterReceived.AddRange(etc);
                                    }
                                    CheckCommand(command);
                                }
                                else
                                {
                                    byte[] command = receive.Slice(s_idx, receive.Length - s_idx);
                                    parameterReceived.Clear();
                                    parameterReceived.AddRange(command);
                                }
                            }
                        }
                    }
                    else
                    {
                        receivedData.AddRange(buffer);
                        if (receivedData.Count >= 57)
                        {
                            byte[] receive = receivedData.ToArray();
                            int s_idx = getNewStxIndex(receive);
                            if (s_idx == 0)
                            {
                                if (receive.Length > 57)
                                {
                                    byte[] cmd = receive.Slice(57);
                                    byte[] etc = receive.Slice(57, receivedData.Count - 57);
                                    receivedData.Clear();
                                    receivedData.AddRange(etc);
                                    CheckCommand(cmd);
                                }
                                else
                                {
                                    receivedData.Clear();
                                    CheckCommand(receive);
                                }
                            }
                            else
                            {
                                if (receive[s_idx - 1] == 0x34)
                                {
                                    byte[] command = receive.Slice(s_idx);
                                    byte[] etc = receive.Slice(s_idx, receive.Length - s_idx);
                                    if (command.Length == 57)
                                    {
                                        receivedData.Clear();
                                        receivedData.AddRange(etc);
                                    }
                                    else
                                    {
                                        receivedData.Clear();
                                        receivedData.AddRange(etc);
                                    }
                                    CheckCommand(command);
                                }
                                else
                                {
                                    byte[] command = receive.Slice(s_idx, receive.Length - s_idx);
                                    receivedData.Clear();
                                    receivedData.AddRange(command);
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                if(Length > 0)
                {
                    byte[] buffer = data.Slice(Length);
                    if (parameterCnt > 0)
                    {
                        parameterReceived.AddRange(buffer);

                        if (parameterReceived.Count >= 70)
                        {
                            byte[] receive = parameterReceived.ToArray();
                            int s_idx = getStxIndex(receive);
                            if (s_idx == 0)
                            {
                                if (receive.Length > 70)
                                {
                                    byte[] cmd = receive.Slice(70);
                                    byte[] etc = receive.Slice(70, parameterReceived.Count - 70);
                                    parameterReceived.Clear();
                                    parameterReceived.AddRange(etc);
                                    CheckCommand(cmd);
                                }
                                else
                                {
                                    parameterReceived.Clear();
                                    CheckCommand(receive);
                                }
                            }
                            else
                            {
                                if (receive[s_idx - 1] == 0xEF)
                                {
                                    byte[] command = receive.Slice(s_idx);
                                    byte[] etc = receive.Slice(s_idx, receive.Length - s_idx);
                                    if (command.Length == 70)
                                    {
                                        parameterReceived.Clear();
                                        parameterReceived.AddRange(etc);
                                    }
                                    else
                                    {
                                        parameterReceived.Clear();
                                        parameterReceived.AddRange(etc);
                                    }
                                    CheckCommand(command);
                                }
                                else
                                {
                                    byte[] command = receive.Slice(s_idx, receive.Length - s_idx);
                                    parameterReceived.Clear();
                                    parameterReceived.AddRange(command);
                                }
                            }
                        }
                    }
                    else
                    {
                        receivedData.AddRange(buffer);
                        if (receivedData.Count >= 57)
                        {
                            byte[] receive = receivedData.ToArray();
                            int s_idx = getStxIndex(receive);
                            if (s_idx == 0)
                            {
                                if(receive.Length > 57)
                                {
                                    byte[] cmd = receive.Slice(57);
                                    byte[] etc = receive.Slice(57, receivedData.Count - 57);
                                    receivedData.Clear();
                                    receivedData.AddRange(etc);
                                    CheckCommand(cmd);
                                }
                                else
                                {
                                    receivedData.Clear();
                                    CheckCommand(receive);
                                }                                
                            }
                            else
                            {
                                if(receive[s_idx - 1] == 0xEF)
                                {
                                    byte[] command = receive.Slice(s_idx);
                                    byte[] etc = receive.Slice(s_idx, receive.Length - s_idx);
                                    if(command.Length == 57)
                                    {
                                        receivedData.Clear();
                                        receivedData.AddRange(etc);
                                    }
                                    else
                                    {
                                        receivedData.Clear();
                                        receivedData.AddRange(etc);
                                    }
                                    CheckCommand(command);
                                }
                                else
                                {
                                    byte[] command = receive.Slice(s_idx, receive.Length - s_idx);
                                    receivedData.Clear();
                                    receivedData.AddRange(command);
                                }
                            }
                        }
                    }
                }
            }
        }

        private int getStxIndex(byte[] data)
        {
            for(int i = 0; i < data.Length; i++)
            {
                if(data[i] == 0xCC)
                {
                    if(i + 2 < data.Length)
                    {
                        if(data[i + 1] == 0x00)
                        {
                            if(data[i + 2] == 0x99 || data[i + 2] == 0xB9 || data[i + 2] == 0xA0 || data[i + 2] == 0xAA)
                            {
                                return i;
                            }
                        }
                    }
                }
            }
            return -1;
        }

        private int getNewStxIndex(byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] == 0x12)
                {
                    if (i + 2 < data.Length)
                    {
                        if (data[i + 1] == 0x01)
                        {
                            if (data[i + 2] == 0x99 || data[i + 2] == 0xB9 || data[i + 2] == 0xA0 || data[i + 2] == 0xAA)
                            {
                                return i;
                            }
                        }
                    }
                }
            }
            return -1;
        }
    }
}
