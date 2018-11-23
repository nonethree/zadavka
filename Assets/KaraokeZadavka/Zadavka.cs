using System.Collections;

using UnityEngine;
using UnityEngine.UI;
using System;
using System.Threading;


public class Zadavka : MonoBehaviour
{
    [SerializeField]
    string in_path;
    [SerializeField]
    string out_path;
    public Text txt;
    public InputField inPath;
    public InputField outPath;
    private AudioSource source;
    private AudioClip clip;
    private AudioClip resultClip;
    private float[] clipSamples;
    private float[] resultClip_samples;
    string currentState = "";

    // Use this for initialization
    void Start()
    {
        source = GetComponent<AudioSource>();
        
    }
    void Update()
    {
        txt.text = currentState;

    }
    #region test app fuctions
    /// <summary>
    /// Button press
    /// </summary>
    public void Process()
    {
        in_path = inPath.text;
        out_path = outPath.text;
        if (System.IO.File.Exists(in_path)) RemoveVocal(in_path);
        else currentState = "File not found";
    }

   
    private void Play(string path)
    {
        StartCoroutine(LoadFile(GetFileURL(path), (clip) => {
            source.clip = clip;
            source.Play();
        },  () => currentState = "Play Error"));
    }

    private void Play(AudioClip clip)
    {
        
            source.clip = clip;
            source.Play();
        
    } 
    public void OnOriginalFileInEndEdit()
    {
        string inputpath = inPath.text;
        var index = inputpath.LastIndexOf('.');
        var filename = inputpath.Substring(0, (index < 0) ? 0:index);
        string outputpath = filename + "_voiceOut.mp3";
        outPath.text = outputpath;
    }
#endregion
    public static string GetFileURL(string path)
    {
        return (new System.Uri(path)).AbsoluteUri;
    }

    IEnumerator LoadFile(string url, Action<AudioClip> loadedClip, Action failLoad)
    {
        using (WWW www = new WWW( url))
        {
            yield return www;
            if (www.error != null) Debug.LogError(www.error);
            else
            {

                clip = www.GetAudioClip();
                if (null != clip)
                {
                    
                    loadedClip(clip);
                }
                else failLoad();
            }

        }
    }

    void Encode()
    {
        currentState = "Encoding";
        EncodeMP3.convert(resultClip_samples, out_path, 128);
    }
   
    /// <summary>
    /// Основная функция, удаляет голос из файла 
    /// </summary>
    /// <param name="path"></param>
    void RemoveVocal(string path)
    {
        //Detect filetype
        string filetype = path.Substring(path.LastIndexOf('.') + 1);
        currentState = "Processing";
        StartCoroutine(LoadFile(GetFileURL(path), (clip) => {
           
            if (clip.channels == 2)
            {
                clipSamples = new float[clip.samples*clip.channels];
                clip.GetData(clipSamples, 0);
                resultClip_samples = new float[clip.samples];
               
                RemoveVocalStereo(clipSamples, ref resultClip_samples, clip.frequency);

                resultClip = AudioClip.Create("resultClip", clip.samples, 1, clip.frequency, false);
                resultClip.SetData(resultClip_samples, 0);
                if (null != resultClip)
                {
                    //Играем результат
                    Play(resultClip);

                    var thread = new Thread(Encode);

                    thread.Start();
                    StartCoroutine(WaitThreadEnd(thread));    
                }
                
               



            }
            else if (clip.channels == 1)
            {
                resultClip = RemoveVocalMono(clip);
                //Играем результат
                if (null != resultClip) {
                    Play(resultClip);

                    var thread = new Thread(Encode);
                    thread.Start();
                    StartCoroutine(WaitThreadEnd(thread));
                }
                
            }

           
            

        }, () => currentState = "Error"));

    }

    IEnumerator WaitThreadEnd(Thread thread)
    {
        while (thread.IsAlive) yield return null;
        currentState = "Done";
    }

    AudioClip RemoveVocalMono(AudioClip clip)
    {
        Debug.Log("Mono");
        float[] mono = new float[clip.samples];
        clip.GetData(mono, 0);
      
        //Просто приглушить средние частоты, с моно трюк с каналами не работает
        var eq = new Equalizer();
        eq.midFreq = 0.0f;
        
        eq.highFreq = 1.5f;
        eq.Init();
        eq.Equalize(ref mono, 1);
        resultClip_samples = mono;
        var summeedClip = AudioClip.Create("summed", clip.samples, 1, clip.frequency, false);
        summeedClip.SetData(mono, 0);
        return summeedClip;
    }



    void RemoveVocalStereo(float[] samples, ref float[] result, int freq )
    {
        Debug.Log("Stereo");
        //Отнять от левого канала правый, голос в центре взаимоуничтожится. Добавить бас чтоб было посочней.
        #region LowPassFilterVars
        float sr = freq;
        float cutoff = 200;
        
        float resonance = 0;
        
        float dryWet = 1;

        //Уровень громкости басовой партии
        float volume = 0.5f;
     
        float ic0 = 0, ic1 = 0, ic2 = 0, oc1 = 0, oc2 = 0;
        float ldi1 = 0, ldi2 = 0, ldo1 = 0, ldo2 = 0;
        
        float dc = 1e-18f;

        float w0 = ((2 * Mathf.PI) * cutoff) / sr;
        float alpha = Mathf.Sin(w0) / 2 * (1 - resonance);
        float cos = Mathf.Cos(w0);
        float b0, b1, b2, a0, a1, a2;
        b0 = (1 - cos) * 0.75f;
        b1 = 1 - cos;
        b2 = b0;
        a0 = 1 + alpha;
        a1 = -2 * cos;
        a2 = 1 - alpha;
        ic0 = b0 / a0;
        ic1 = b1 / a0;
        ic2 = b2 / a0;
        oc1 = -(a1 / a0);
        oc2 = -(a2 / a0);
        #endregion
        Debug.Log("Process");
        for (int i = 0; i < samples.Length; i += 2)
        {
            #region LowPassFilter
            var mono = (samples[i] + samples[i+1]) / 2;
            float ly = ic0 * mono + ic1 * ldi1 + ic2 * ldi2 + oc1 * ldo1 + oc2 * ldo2 + dc;
            ldi2 = ldi1;
            ldi1 = mono;
            ldo2 = ldo1;
            ldo1 = ly;
            mono = volume * (mono * (1 - dryWet) + (dryWet) * ly);
            #endregion

            result[i/2] = (samples[i] - samples[i+1])/2 + mono;
            
        }
        Debug.Log("Processing end");
        
    }
    void LogClipInfo(AudioClip clip)
    {
        var channels = clip.channels;
        var name = clip.name;
        var samples = clip.samples;
        var freq = clip.frequency;
        Debug.Log(name + " " +
                  "Samples: " + samples + " " +
                  "Channels: " + channels + " " +
                  "Frequerency: " + freq);
    }

    
   
    public class LowPassFilter
    {
        public float sr = 44000;

        [Range(100, 20000)]
        public float cutoff = 20000;
        private float cutoffwas = 1;

        [Range(0.0f, 0.95f)]
        public float resonance = 0;
        private float resonancewas = 1;

        [Range(0, 1)]
        public float dryWet = 1;

        [Range(0, 1)]
        public float volume = 1f;

        private float ic0 = 0, ic1 = 0, ic2 = 0, oc1 = 0, oc2 = 0;
        private float ldi1 = 0, ldi2 = 0, ldo1 = 0, ldo2 = 0;
        private float rdi1 = 0, rdi2 = 0, rdo1 = 0, rdo2 = 0;
        private const float dc = 1e-18f;

        public void Filter(ref float[] data, int channels)
        {
            if (cutoffwas != cutoff || resonancewas != resonance)
            {
                float w0 = ((2 * Mathf.PI) * cutoff) / sr;
                float alpha = Mathf.Sin(w0) / 2 * (1 - resonance);
                float cos = Mathf.Cos(w0);
                float b0, b1, b2, a0, a1, a2;
                b0 = (1 - cos) * 0.5f;
                b1 = 1 - cos;
                b2 = b0;
                a0 = 1 + alpha;
                a1 = -2 * cos;
                a2 = 1 - alpha;
                ic0 = b0 / a0;
                ic1 = b1 / a0;
                ic2 = b2 / a0;
                oc1 = -(a1 / a0);
                oc2 = -(a2 / a0);
            }
            cutoffwas = cutoff;
            resonancewas = resonance;
            if (channels == 2)
            {
                for (int i = 0; i < data.Length; i += 2)
                {
                    float ly = ic0 * data[i] + ic1 * ldi1 + ic2 * ldi2 + oc1 * ldo1 + oc2 * ldo2 + dc;
                    float ry = ic0 * data[i + 1] + ic1 * rdi1 + ic2 * rdi2 + oc1 * rdo1 + oc2 * rdo2 + dc;
                    ldi2 = ldi1;
                    ldi1 = data[i];
                    ldo2 = ldo1;
                    ldo1 = ly;

                    rdi2 = rdi1;
                    rdi1 = data[i + 1];
                    rdo2 = rdo1;
                    rdo1 = ry;

                    data[i] = volume * (data[i] * (1 - dryWet) + (dryWet) * ly);
                    data[i + 1] = volume * (data[i + 1] * (1 - dryWet) + (dryWet) * ry);
                }
            }
            else
            {
                for (int i = 0; i < data.Length; i++)
                {
                    float ly = ic0 * data[i] + ic1 * ldi1 + ic2 * ldi2 + oc1 * ldo1 + oc2 * ldo2 + dc;
                    ldi2 = ldi1;
                    ldi1 = data[i];
                    ldo2 = ldo1;
                    ldo1 = ly;
                    data[i] = volume * (data[i] * (1 - dryWet) + (dryWet) * ly);
                }
            }

        }
    }

    public class HighPassFilter
    {
        public float sr = 44000;

        [Range(10, 20000)]
        public float cutoff = 100;
        private float cutoffwas = 1;

        [Range(0.0f, 0.95f)]
        public float resonance = 0;
        private float resonancewas = 1;

        [Range(0, 1)]
        public float dryWet = 1;

        [Range(0, 1)]
        public float volume = 0.9f;

        private float ic0 = 0, ic1 = 0, ic2 = 0, oc1 = 0, oc2 = 0;
        private float ldi1 = 0, ldi2 = 0, ldo1 = 0, ldo2 = 0;
        private float rdi1 = 0, rdi2 = 0, rdo1 = 0, rdo2 = 0;
        private const float dc = 1e-18f;

        public void Filter(ref float[] data, int channels)
        {
            if (cutoffwas != cutoff || resonancewas != resonance)
            {
                float w0 = ((2 * Mathf.PI) * cutoff) / sr;
                float alpha = Mathf.Sin(w0) / 2 * (1 - resonance);
                float cos = Mathf.Cos(w0);
                float b0, b1, b2, a0, a1, a2;
                b0 = (1 + cos) * 0.5f;
                b1 = -(1 + cos);
                b2 = b0;
                a0 = 1 + alpha;
                a1 = -2 * cos;
                a2 = 1 - alpha;
                ic0 = b0 / a0;
                ic1 = b1 / a0;
                ic2 = b2 / a0;
                oc1 = -(a1 / a0);
                oc2 = -(a2 / a0);
            }
            cutoffwas = cutoff;
            resonancewas = resonance;
            if (channels == 2)
            {
                for (int i = 0; i < data.Length; i += 2)
                {
                    float ly = ic0 * data[i] + ic1 * ldi1 + ic2 * ldi2 + oc1 * ldo1 + oc2 * ldo2 + dc;
                    float ry = ic0 * data[i + 1] + ic1 * rdi1 + ic2 * rdi2 + oc1 * rdo1 + oc2 * rdo2 + dc;
                    ldi2 = ldi1;
                    ldi1 = data[i];
                    ldo2 = ldo1;
                    ldo1 = ly;

                    rdi2 = rdi1;
                    rdi1 = data[i + 1];
                    rdo2 = rdo1;
                    rdo1 = ry;

                    data[i] = volume * (data[i] * (1 - dryWet) + (dryWet) * ly);
                    data[i + 1] = volume * (data[i + 1] * (1 - dryWet) + (dryWet) * ry);
                }
            }
            else
            {
                for (int i = 0; i < data.Length; i++)
                {
                    float ly = ic0 * data[i] + ic1 * ldi1 + ic2 * ldi2 + oc1 * ldo1 + oc2 * ldo2 + dc;
                    ldi2 = ldi1;
                    ldi1 = data[i];
                    ldo2 = ldo1;
                    ldo1 = ly;
                    data[i] = volume * (data[i] * (1 - dryWet) + (dryWet) * ly);
                }
            }
        }
    }

    public class Equalizer 
    {

        
        [Range(0, 2)]
        public float lowFreq = 1.0f;
        [Range(20, 20000)]
        public int lowFreqEq = 100;
        [Range(0, 2)]
        public float midFreq = 1.0f;
        [Range(20, 20000)]
        public int midFreqEq = 1000;
        [Range(0, 2)]
        public float highFreq = 1.0f;
        [Range(20, 20000)]
        public int highFreqEq = 10000;

        public struct EQSTATE
        {
            public float lf;
            public float f1p0;
            public float f1p1;
            public float f1p2;
            public float f1p3;

            public float hf;
            public float f2p0;
            public float f2p1;
            public float f2p2;
            public float f2p3;

            public float sdm1;
            public float sdm2;
            public float sdm3;

            public float lg;
            public float mg;
            public float hg;

        };

        static float vsa = (1.0f / 4294967295.0f);

        EQSTATE eq = new EQSTATE();

        public void Init()
        {
            init_3band_state(ref eq, lowFreqEq, midFreqEq, highFreqEq);
            eq.lg = lowFreq;
            eq.mg = midFreq;
            eq.hg = highFreq;
        }

        

        public void Equalize(ref float[] data, int channels)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = do_3band(ref eq, data[i]);
            }
        }

        void init_3band_state(ref EQSTATE es, int lowfreq, int highfreq, int mixfreq)
        {
            es.lg = 1.0f; es.mg = 1.0f; es.hg = 1.0f;
            es.lf = 2f * Mathf.Sin(Mathf.PI * ((float)lowfreq / (float)mixfreq));
            es.hf = 2f * Mathf.Sin(Mathf.PI * ((float)highfreq / (float)mixfreq));
        }

        float do_3band(ref EQSTATE es, float sample)
        {
            float l, m, h;
            es.f1p0 += (es.lf * (sample - es.f1p0)) + vsa;
            es.f1p1 += (es.lf * (es.f1p0 - es.f1p1));
            es.f1p2 += (es.lf * (es.f1p1 - es.f1p2));
            es.f1p3 += (es.lf * (es.f1p2 - es.f1p3));
            l = es.f1p3;
            es.f2p0 += (es.hf * (sample - es.f2p0)) + vsa;
            es.f2p1 += (es.hf * (es.f2p0 - es.f2p1));
            es.f2p2 += (es.hf * (es.f2p1 - es.f2p2));
            es.f2p3 += (es.hf * (es.f2p2 - es.f2p3));
            h = es.sdm3 - es.f2p3;
            m = es.sdm3 - (h + l);
            l *= es.lg;
            m *= es.mg;
            h *= es.hg;
            es.sdm3 = es.sdm2;
            es.sdm2 = es.sdm1;
            es.sdm1 = sample;
            return (l + m + h);
        }
    }

}

