using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using System;
using System.IO;

[StructLayout(LayoutKind.Sequential)]
public struct PTDat
{
    public bool tf;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst =3)]
    public double[] trsh1;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public double[] trsh2;
    public int level;
}
enum PROTOCOL
{
    Alpha, AlphaLow, AlphaHi, AlphaTheta, Smr, SmrBetaLow, BetaLow, BetaMid
}
public class NFTest : MonoBehaviour
{
    [DllImport("nativepFilter")]
    private static extern double parsingData(int first, int second, int third);
    [DllImport("nativepFilter")]
    private static extern IntPtr notch60Lowpass50High1(double[] data);
    [DllImport("nativepFilter")]
    private static extern IntPtr notch60DoubleLowpass50High1(double[] data);
    [DllImport("nativepFilter")]
    private static extern IntPtr notch60TripleLowpass50High1(double[] data);
    [DllImport("nativepFilter")]
    private static extern IntPtr notchTest(double[] data);
    [DllImport("nativepFilter")]
    private static extern void DeleteFData();
    [DllImport("nativepFilter")]
    private static extern IntPtr pBandAbs(double[] raw1);
    [DllImport("nativepFilter")]
    private static extern IntPtr fft(double[] raw);
    [DllImport("NF")]
    private static extern bool protocol2ch(PROTOCOL pt, double[] band1, double[] band2);
    [DllImport("NF")]
    private static extern bool alpha2ch(double[] band1, double[] band2);
    [DllImport("NF")]
    private static extern bool smr2ch(double[] band1, double[] band2);
    [DllImport("NF")]
    private static extern double meditState(double[] band1, double[] band2);
    [DllImport("StructTest")]
    private static extern PTDat proTest1(PROTOCOL pt);
    [DllImport("StructTest")]
    private static extern PTDat proTest2(PROTOCOL pt, double[] band);

    const int SamplingRate = 250;
    const int dataSize = 62;
    const int packetSize = 10;
    int preId = 0;
    private double[] Ch1DataArrayToFiltering = new double[SamplingRate];
    private double[] Ch2DataArrayToFiltering = new double[SamplingRate];
    private Queue<double> Ch1Buffer = new Queue<double>();
    private Queue<double> Ch2Buffer = new Queue<double>();
    //private Queue<double> Ch1FFTBuffer = new Queue<double>();
    //private Queue<double> Ch2FFTBuffer = new Queue<double>();
    //private double[] Ch1BandAlpha = new double[14];
    //private double[] Ch2BandAlpha = new double[14];
    private double[] ch1_preData = new double[10];
    private double[] ch2_preData = new double[10];

    public void ReadTextFile()
    {
        int count = 1;
        TextAsset sourcefile = Resources.Load<TextAsset>("bytes");
        String Path = Application.persistentDataPath + "/Panxtos";
        FileInfo f = new FileInfo(Path + "/protoAlphaBool.txt");
        StreamWriter w;
        if(!Directory.Exists(Path))
        {
            Directory.CreateDirectory(Path);
            Debug.Log("Create Directory");
        }
        else
        {
            Debug.Log("already Directory Exist");
        }

        if (!f.Exists)
        {
            w = f.CreateText();
            Debug.Log("Create success");
        }
        else
        {
            f.Delete();
            w = f.CreateText();
        }
        if (sourcefile != null)
        {
            string str = sourcefile.text;
            TextReader txtreader = new StringReader(str);
            string line;

            int ch1_first, ch1_second, ch1_third;
            int ch2_first, ch2_second, ch2_third;
            double ch1_mV, ch2_mV;

            while ((line = txtreader.ReadLine()) != null)
            {

                byte[] bytes = new byte[line.Length / 2];

                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] = Convert.ToByte(line.Substring(i * 2, 2), 16);
                }
                for (int i = 1; i < dataSize / 2; i += 3)
                {
                    ch1_first = bytes[i];
                    ch1_second = bytes[i + 1];
                    ch1_third = bytes[i + 2];
                    ch1_mV = parsingData(ch1_first, ch1_second, ch1_third);
                    ch1_preData[(i - 1) / 3] = ch1_mV;
                }
                for (int i = dataSize / 2 + 1; i < dataSize; i += 3)
                {
                    ch2_first = bytes[i];
                    ch2_second = bytes[i + 1];
                    ch2_third = bytes[i + 2];
                    ch2_mV = parsingData(ch2_first, ch2_second, ch2_third);
                    ch2_preData[(i - 32) / 3] = ch2_mV;
                }
                for (int i = 0; i < 240; i++)
                {
                    Ch1DataArrayToFiltering[i] = Ch1DataArrayToFiltering[i + packetSize];
                    Ch2DataArrayToFiltering[i] = Ch2DataArrayToFiltering[i + packetSize];
                }
                for (int i = 0; i < packetSize; i++)
                {
                    Ch1DataArrayToFiltering[240 + i] = ch1_preData[i];
                    Ch2DataArrayToFiltering[240 + i] = ch2_preData[i];
                }
                double[] filteredCh1Data = new double[10];
                double[] filteredCh2Data = new double[10];
                IntPtr ch1ptr = notch60TripleLowpass50High1(Ch1DataArrayToFiltering);
                Marshal.Copy(ch1ptr, filteredCh1Data, 0, 10);
                IntPtr ch2ptr = notch60TripleLowpass50High1(Ch2DataArrayToFiltering);
                Marshal.Copy(ch2ptr, filteredCh2Data, 0, 10);
                for (int i = 0; i < packetSize; i++)
                {
                    Ch1Buffer.Enqueue(filteredCh1Data[i]);
                    Ch2Buffer.Enqueue(filteredCh2Data[i]);
                }
                if (Ch1Buffer.Count >= 250)
                {
                    double[] raw1 = new double[250];
                    double[] raw2 = new double[250];
                    for (int i = 0; i < 250; i++)
                    {
                        raw1[i] = Ch1Buffer.Dequeue();
                        raw2[i] = Ch2Buffer.Dequeue();
                    }
                    double[] abs1 = new double[14];
                    double[] abs2 = new double[14];
                    IntPtr ch1BandAbs = pBandAbs(raw1);
                    IntPtr ch2BandAbs = pBandAbs(raw2);
                    Marshal.Copy(ch1BandAbs, abs1, 0, 14);
                    Marshal.Copy(ch2BandAbs, abs2, 0, 14);

                    //double[] Ch1Band = BandAbs(raw1);
                    //double[] Ch2Band = BandAbs(raw2);
                    PROTOCOL pt = PROTOCOL.Alpha;
                    bool relaxValue = protocol2ch(pt, abs1, abs2);
                    //BTtext.text = "Relax " + relaxValue;
                    //Debug.Log("trsh1 : " + relaxValue.trsh1[0] + ", " + relaxValue.trsh1[1] + ", " + relaxValue.trsh1[2]);
                    //Debug.Log("trsh2 : " + relaxValue.trsh2[0] + ", " + relaxValue.trsh2[1] + ", " + relaxValue.trsh2[2]);

                    //w.WriteLine(relaxValue.trsh2[0] + ", " + relaxValue.trsh2[1] + ", " + relaxValue.trsh2[2]);
                    Debug.Log("Bool :" + relaxValue + "  Count :  " + count);
                    string print = "Flag=";
                    print += relaxValue;
                    print += "  IT =";
                    print += count;
                    w.WriteLine(print);
                    count++;
                }

            }
        }
        w.Close();
    }

    public void ReadRawDataFile()
    {
        TextAsset sourcefile = Resources.Load<TextAsset>("bytesToCh1uV");
        TextAsset sourcefile2 = Resources.Load<TextAsset>("bytesToCh2uV");
        //String Path = Application.persistentDataPath + "/Panxtos";
        //FileInfo f = new FileInfo(Path + "/protoAlphaBool.txt");
        if (sourcefile != null)
        {
            string str = sourcefile.text;
            string str2 = sourcefile2.text;
            TextReader txtreader = new StringReader(str);
            TextReader txtreader2 = new StringReader(str2);
            string line, line2;

            //int ch1_first, ch1_second, ch1_third;
            //int ch2_first, ch2_second, ch2_third;
            //double ch1_mV, ch2_mV;
            for (int it = 0; it < 320; it++)
            {
                double[] raw1 = new double[250];
                double[] raw2 = new double[250];
                for (int i=0; i< 250; i++)
                {
                    line = txtreader.ReadLine();
                    raw1[i] = Double.Parse(line);
                }
                for (int i = 0; i < 250; i++)
                {
                    line2 = txtreader2.ReadLine();
                    raw2[i] = Double.Parse(line2);
                }
                double[] fftCh1 = new double[125];
                double[] fftCh2 = new double[125];
                IntPtr ch1FFTptr = fft(raw1);
                IntPtr ch2FFTptr = fft(raw2);
                Marshal.Copy(ch1FFTptr, fftCh1, 0, 125);
                Marshal.Copy(ch2FFTptr, fftCh2, 0, 125);
                //Debug.Log("Ch1 FFT:  " + fftCh1[0] + "    Ch124 FFT   : " + fftCh1[1] + "    " + fftCh1[2] + "    " + fftCh1[3] + "    " + fftCh1[4] + "    " + fftCh1[5]);
                double[] abs1 = new double[14];
                double[] abs2 = new double[14];
                IntPtr ch1BandAbs = pBandAbs(raw1);
                IntPtr ch2BandAbs = pBandAbs(raw2);
                Marshal.Copy(ch1BandAbs, abs1, 0, 14);
                Marshal.Copy(ch2BandAbs, abs2, 0, 14);
                //Debug.Log("Ch1 BandAbs : " + abs1[0] + " Ch2 BandAbs : " + abs2[0]);
                bool TF = false;
                PROTOCOL pt = PROTOCOL.Alpha;
                double mediresult = meditState(abs1, abs2);
                Debug.Log("medi : " + mediresult);
                //TF = protocol2ch(pt, abs1, abs2);
                //TF = alpha2ch(abs1, abs2);
                //TF = smr2ch(abs1, abs2);
                //if (TF == true)
                //{
                //    int tt = it + 1;
                //    Debug.Log("TRUE  :  " + tt);
                //}
            }


        }
    }
    public void StructTest()
    {
        PROTOCOL pt = PROTOCOL.AlphaLow;
        PTDat a = proTest1(pt);
        Debug.Log("level : " + a.level);
        Debug.Log("Bool : " + a.tf);
        Debug.Log("trsh1 : " + a.trsh1[0] + a.trsh1[1] + a.trsh1[2]);
        
        PROTOCOL pt2 = PROTOCOL.AlphaHi;
        PTDat a2 = proTest1(pt2);
        Debug.Log("level : " + a2.level);
        
        PROTOCOL pt3 = PROTOCOL.AlphaTheta;
        PTDat a3 = proTest1(pt3);
        Debug.Log("level : " + a3.level);
        
        PROTOCOL pt4 = PROTOCOL.Smr;
        PTDat a4 = proTest1(pt4);
        Debug.Log("level : " + a4.level);
        
        PROTOCOL pt5 = PROTOCOL.SmrBetaLow;
        PTDat a5 = proTest1(pt5);
        Debug.Log("level : " + a5.level);
    }

    public void FilterTest()
    {
        const int samplingRate = 250;
        double[] raw = new double[samplingRate];
        double[] t = new double[samplingRate];
        for (int i = 0; i < samplingRate; i++)
        {
            t[i] = 1.0 / samplingRate * i;
            raw[i] = 1000 * Math.Sin(2.0 * 60 * Math.PI * t[i]);
        }
        double[] result = new double[10];
        IntPtr resultFilter = notchTest(raw);
        Marshal.Copy(resultFilter, result, 0, 10);
        for (int i = 0; i < 10; i++)
        {
            Debug.Log(result[i]);
        }
    }
    public void ReadTextFile2()
    {
        int count = 1;
        TextAsset sourcefile = Resources.Load<TextAsset>("bytes");
        String Path = Application.persistentDataPath + "/Panxtos";
        FileInfo f = new FileInfo(Path + "/protoAlphaBool.txt");
        StreamWriter w;
        if (!Directory.Exists(Path))
        {
            Directory.CreateDirectory(Path);
            Debug.Log("Create Directory");
        }
        else
        {
            Debug.Log("already Directory Exist");
        }

        if (!f.Exists)
        {
            w = f.CreateText();
            Debug.Log("Create success");
        }
        else
        {
            f.Delete();
            w = f.CreateText();
        }
        if (sourcefile != null)
        {
            string str = sourcefile.text;
            TextReader txtreader = new StringReader(str);
            string line;

            int ch1_first, ch1_second, ch1_third;
            int ch2_first, ch2_second, ch2_third;
            double ch1_mV, ch2_mV;

            while ((line = txtreader.ReadLine()) != null)
            {

                byte[] bytes = new byte[line.Length / 2];

                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] = Convert.ToByte(line.Substring(i * 2, 2), 16);
                }
                for (int i = 1; i < dataSize / 2; i += 3)
                {
                    ch1_first = bytes[i];
                    ch1_second = bytes[i + 1];
                    ch1_third = bytes[i + 2];
                    ch1_mV = parsingData(ch1_first, ch1_second, ch1_third);
                    ch1_preData[(i - 1) / 3] = ch1_mV;
                }
                for (int i = dataSize / 2 + 1; i < dataSize; i += 3)
                {
                    ch2_first = bytes[i];
                    ch2_second = bytes[i + 1];
                    ch2_third = bytes[i + 2];
                    ch2_mV = parsingData(ch2_first, ch2_second, ch2_third);
                    ch2_preData[(i - 32) / 3] = ch2_mV;
                }
                for (int i = 0; i < 240; i++)
                {
                    Ch1DataArrayToFiltering[i] = Ch1DataArrayToFiltering[i + packetSize];
                    Ch2DataArrayToFiltering[i] = Ch2DataArrayToFiltering[i + packetSize];
                }
                for (int i = 0; i < packetSize; i++)
                {
                    Ch1DataArrayToFiltering[240 + i] = ch1_preData[i];
                    Ch2DataArrayToFiltering[240 + i] = ch2_preData[i];
                }
                double[] filteredCh1Data = new double[10];
                double[] filteredCh2Data = new double[10];
                IntPtr ch1ptr = notchTest(Ch1DataArrayToFiltering);
                IntPtr ch2ptr = notchTest(Ch2DataArrayToFiltering);
                Marshal.Copy(ch1ptr, filteredCh1Data, 0, 10);
                Marshal.Copy(ch2ptr, filteredCh2Data, 0, 10);
                for (int i = 0; i < packetSize; i++)
                {
                    Ch1Buffer.Enqueue(filteredCh1Data[i]);
                    Ch2Buffer.Enqueue(filteredCh2Data[i]);
                }
                if (Ch1Buffer.Count >= 250)
                {
                    double[] raw1 = new double[250];
                    double[] raw2 = new double[250];
                    for (int i = 0; i < 250; i++)
                    {
                        raw1[i] = Ch1Buffer.Dequeue();
                        raw2[i] = Ch2Buffer.Dequeue();
                    }
                    double[] abs1 = new double[14];
                    double[] abs2 = new double[14];
                    IntPtr ch1BandAbs = pBandAbs(raw1);
                    IntPtr ch2BandAbs = pBandAbs(raw2);
                    Marshal.Copy(ch1BandAbs, abs1, 0, 14);
                    Marshal.Copy(ch2BandAbs, abs2, 0, 14);

                    //double[] Ch1Band = BandAbs(raw1);
                    //double[] Ch2Band = BandAbs(raw2);
                    PROTOCOL pt = PROTOCOL.Alpha;
                    bool relaxValue = protocol2ch(pt, abs1, abs2);
                    //BTtext.text = "Relax " + relaxValue;
                    //Debug.Log("trsh1 : " + relaxValue.trsh1[0] + ", " + relaxValue.trsh1[1] + ", " + relaxValue.trsh1[2]);
                    //Debug.Log("trsh2 : " + relaxValue.trsh2[0] + ", " + relaxValue.trsh2[1] + ", " + relaxValue.trsh2[2]);

                    //w.WriteLine(relaxValue.trsh2[0] + ", " + relaxValue.trsh2[1] + ", " + relaxValue.trsh2[2]);
                    Debug.Log("Bool :" + relaxValue + "  Count :  " + count);
                    string print = "Flag=";
                    print += relaxValue;
                    print += "  IT =";
                    print += count;
                    w.WriteLine(print);
                    count++;
                }

            }
        }
        w.Close();
    }
}
