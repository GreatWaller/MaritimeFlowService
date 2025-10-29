using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using MaritimeFlowService.Utils;
using H3Util;

namespace MaritimeFlowService.Engine
{
    /// <summary>
    /// ��ʹ�� H3 ģ���ж�λ���쳣�� Condition��
    /// - ��Ҫ���� ModelFolder ָ����� H3 ת��ģ�͵� JSON �ļ��У�h3_transition_model_class_*.json��
    /// - ά��ÿ��ʵ����һ��λ��/ʱ���Ա��뵱ǰλ�Ƶ���ģ���ж�
    /// </summary>
    internal class PositionAnomalyCondition : Condition
    {
        // ָ�� H3 ģ������Ŀ¼����������ⲿ��ǰ����ģ�ͣ�
        public string? ModelFolder { get; set; }

        // ÿ��ʵ���ϴ��ϱ���ʱ����λ�ã��̰߳�ȫ��
        //private static readonly ConcurrentDictionary<string, (DateTime Time, (double Lat, double Lon) Loc)> lastSeen
        //    = new(StringComparer.Ordinal);

        // VesselPrediction ʵ����ģ�ͼ��ؿ��ƣ�������һ�Σ�
        private static readonly VesselPrediction vp = new();
        private static readonly object modelLoadLock = new();
        private static bool modelsLoaded = false;

        public override bool Evaluate(MaritimeEvent ev, RuleState state, RuleEngine engine)
        {
            if (ev == null) return false;

            // ȷ��ģ���Ѽ��أ�����ṩ�� ModelFolder��
            if (!modelsLoaded)
            {
                if (string.IsNullOrWhiteSpace(ModelFolder))
                {
                    Console.WriteLine("PositionAnomalyCondition: ModelFolder δ���ã��޷�ʹ�� H3 ģ�ͽ��м�⡣");
                }
                else
                {
                    lock (modelLoadLock)
                    {
                        if (!modelsLoaded)
                        {
                            try
                            {
                                vp.LoadModels(ModelFolder!);
                                modelsLoaded = true;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"PositionAnomalyCondition: ����ģ��ʧ��: {ex.Message}");
                                // ���׳���������������ȫ���� false
                            }
                        }
                    }
                }
            }

            // ���ģ��δ���أ��޷��ж��쳣
            if (!modelsLoaded)
                return false;

            var loc = ev.Location;

            // ���� COG�����򣩺� VesselType��vtype��
            double cog = 0.0;
            if (ev.Attributes != null)
            {
                if (ev.Attributes.TryGetValue("COG", out var cogVal) || ev.Attributes.TryGetValue("Cog", out cogVal)
                    || ev.Attributes.TryGetValue("course", out cogVal) || ev.Attributes.TryGetValue("Course", out cogVal))
                {
                    double.TryParse(cogVal?.ToString(), out cog);
                }
            }

            int vtype = 0;
            if (ev.Attributes != null)
            {
                if (ev.Attributes.TryGetValue("VesselType", out var vt) || ev.Attributes.TryGetValue("vtype", out vt)
                    || ev.Attributes.TryGetValue("VesselTypeCode", out vt))
                {
                    int.TryParse(vt?.ToString(), out vtype);
                }
            }

            try
            {
                var res = vp.CheckAnomaly(loc.Lat, loc.Lon, cog, vtype);
                if (res != null && res.TryGetValue("is_anomaly", out var isAnomObj)
                    && bool.TryParse(isAnomObj?.ToString(), out var isAnom) && isAnom)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PositionAnomalyCondition: H3 ������ʧ��: {ex.Message}");
                // ģ�ʹ��󰴰�ȫ���Է��� false
            }

            return false;
        }
    }
}