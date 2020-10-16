using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class TesseractWrapper
{
#if UNITY_EDITOR
    private const string TesseractDllName = "tesseract50";
    private const string LeptonicaDllName = "tesseract50";
#elif UNITY_ANDROID
    private const string TesseractDllName = "libtesseract.so";
    private const string LeptonicaDllName = "liblept.so";
#else
    private const string TesseractDllName = "tesseract";
    private const string LeptonicaDllName = "tesseract";
#endif
    [DllImport("tesseract50")]
    private static extern IntPtr TessVersion();

    [DllImport("tesseract50")]
    private static extern IntPtr TessBaseAPICreate();

    [DllImport("tesseract50")]
    private static extern int TessBaseAPIInit3(IntPtr handle, string dataPath, string language);

    [DllImport("tesseract50")]
    private static extern void TessBaseAPIDelete(IntPtr handle);

    [DllImport("tesseract50")]
    private static extern void TessBaseAPIEnd(IntPtr handle);

    [DllImport("tesseract50")]
    private static extern void TessBaseAPISetImage(IntPtr handle, IntPtr
             imagedata, int width, int height,
             int bytes_per_pixel, int bytes_per_line);

    [DllImport("tesseract50")]
    private static extern void TessBaseAPISetImage2(IntPtr handle,
                 IntPtr pix);

    [DllImport("tesseract50")]
    private static extern int TessBaseAPIRecognize(IntPtr handle, IntPtr
                 monitor);

    [DllImport("tesseract50")]
    private static extern IntPtr TessBaseAPIGetUTF8Text(IntPtr handle);

    [DllImport("tesseract50")]
    private static extern void TessDeleteText(IntPtr text);

    [DllImport("tesseract50")]
    private static extern void TessBaseAPIClear(IntPtr handle);

    [DllImport("tesseract50")]
    private static extern IntPtr TessBaseAPIGetWords(IntPtr handle, IntPtr pixa);

    IntPtr _tessHandle;
    private string _errorMsg;

    public TesseractWrapper()
    {
        _tessHandle = IntPtr.Zero;
    }


    public string Version()
    {
        IntPtr strPtr = TessVersion();
        string tessVersion = Marshal.PtrToStringAnsi(strPtr);
        return tessVersion;
    }

    public string GetErrorMessage()
    {
        return _errorMsg;
    }

    public bool Init(string lang, string dataPath)
    {
        if (!_tessHandle.Equals(IntPtr.Zero))
            Close();

        try
        {
            _tessHandle = TessBaseAPICreate();
            if (_tessHandle.Equals(IntPtr.Zero))
            {
                _errorMsg = "TessAPICreate failed";
                return false;
            }

            if (string.IsNullOrWhiteSpace(dataPath))
            {
                _errorMsg = "Invalid DataPath";
                return false;
            }

            int init = TessBaseAPIInit3(_tessHandle, dataPath,
                    lang);
            if (init != 0)
            {
                Close();
                _errorMsg = "TessAPIInit failed. Output: " + init;
                return false;
            }
        }
        catch (Exception ex)
        {
            _errorMsg = ex + " -- " + ex.Message;
            return false;
        }

        return true;
    }

    public void Close()
    {
        if (_tessHandle.Equals(IntPtr.Zero))
            return;
        TessBaseAPIEnd(_tessHandle);
        TessBaseAPIDelete(_tessHandle);
        _tessHandle = IntPtr.Zero;
    }

    private Texture2D _highlightedTexture;

    public string Recognize(Texture2D texture)
    {
        if (_tessHandle.Equals(IntPtr.Zero))
            return null;

        int width = texture.width;
        int height = texture.height;
        Color32[] colors = texture.GetPixels32();
        int count = width * height;
        int bytesPerPixel = 4;
        byte[] dataBytes = new byte[count * bytesPerPixel];

        int bytePtr = 0;
        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = 0; x < width; x++)
            {
                int colorIdx = y * width + x;
                dataBytes[bytePtr++] = colors[colorIdx].r;
                dataBytes[bytePtr++] = colors[colorIdx].g;
                dataBytes[bytePtr++] = colors[colorIdx].b;
                dataBytes[bytePtr++] = colors[colorIdx].a;
            }
        }

        IntPtr imagePtr = Marshal.AllocHGlobal(count * bytesPerPixel);
        Marshal.Copy(dataBytes, 0, imagePtr, count * bytesPerPixel);
        TessBaseAPISetImage(_tessHandle, imagePtr, width, height,
                        bytesPerPixel, width * bytesPerPixel);

        if (TessBaseAPIRecognize(_tessHandle, IntPtr.Zero) != 0)
        {
            Marshal.FreeHGlobal(imagePtr);
            return null;
        }

        int pointerSize = Marshal.SizeOf(typeof(IntPtr));
        IntPtr intPtr = TessBaseAPIGetWords(_tessHandle, IntPtr.Zero);
        Boxa boxa = Marshal.PtrToStructure<Boxa>(intPtr);
        Box[] boxes = new Box[boxa.n];
        for (int index = 0; index < boxes.Length; index++)
        {
            IntPtr boxPtr = Marshal.ReadIntPtr(boxa.box,
                                           index * pointerSize);
            boxes[index] = Marshal.PtrToStructure<Box>(boxPtr);
            Box box = boxes[index];
            DrawLines(texture,
                 new Rect(box.x, texture.height - box.y - box.h, box.w, box.h),
                 Color.green);
        }

        IntPtr str_ptr = TessBaseAPIGetUTF8Text(_tessHandle);
        Marshal.FreeHGlobal(imagePtr);
        if (str_ptr.Equals(IntPtr.Zero))
            return null;
    #if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        string recognizedText = Marshal.PtrToStringAnsi(str_ptr);
    #else
    string recognizedText = Marshal.PtrToStringAuto(str_ptr);
    #endif

        TessBaseAPIClear(_tessHandle);
        TessDeleteText(str_ptr);

        return recognizedText;
    }

    private void DrawLines(Texture2D texture, Rect boundingRect, Color
               color, int thickness = 3)
    {
        int x1 = (int)boundingRect.x;
        int x2 = (int)(boundingRect.x + boundingRect.width);
        int y1 = (int)boundingRect.y;
        int y2 = (int)(boundingRect.y + boundingRect.height);

        for (int x = x1; x <= x2; x++)
        {
            for (int i = 0; i < thickness; i++)
            {
                texture.SetPixel(x, y1 + i, color);
                texture.SetPixel(x, y2 - i, color);
            }
        }

        for (int y = y1; y <= y2; y++)
        {
            for (int i = 0; i < thickness; i++)
            {
                texture.SetPixel(x1 + i, y, color);
                texture.SetPixel(x2 - i, y, color);
            }
        }

        texture.Apply();
    }

    public Texture2D GetHighlightedTexture()
    {
        return _highlightedTexture;
    }
}