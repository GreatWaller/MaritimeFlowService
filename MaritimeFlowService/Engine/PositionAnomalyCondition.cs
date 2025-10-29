using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using MaritimeFlowService.Utils;
using H3Util;

namespace MaritimeFlowService.Engine
{
    /// <summary>
    /// 仅使用 H3 模型判断位置异常的 Condition。
    /// - 需要设置 ModelFolder 指向包含 H3 转移模型的 JSON 文件夹（h3_transition_model_class_*.json）
    /// - 维护每个实体上一次位置/时间以便与当前位移调用模型判断
    /// </summary>
    internal class PositionAnomalyCondition : Condition
    {
        // 指定 H3 模型所在目录（必填，或者外部提前加载模型）
        public string? ModelFolder { get; set; }

        // 每个实体上次上报的时间与位置（线程安全）
        //private static readonly ConcurrentDictionary<string, (DateTime Time, (double Lat, double Lon) Loc)> lastSeen
        //    = new(StringComparer.Ordinal);

        // VesselPrediction 实例与模型加载控制（仅加载一次）
        private static readonly VesselPrediction vp = new();
        private static readonly object modelLoadLock = new();
        private static bool modelsLoaded = false;

        public override bool Evaluate(MaritimeEvent ev, RuleState state, RuleEngine engine)
        {
            if (ev == null) return false;

            // 确保模型已加载（如果提供了 ModelFolder）
            if (!modelsLoaded)
            {
                if (string.IsNullOrWhiteSpace(ModelFolder))
                {
                    Console.WriteLine("PositionAnomalyCondition: ModelFolder 未设置，无法使用 H3 模型进行检测。");
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
                                Console.WriteLine($"PositionAnomalyCondition: 加载模型失败: {ex.Message}");
                                // 不抛出，后续评估将安全返回 false
                            }
                        }
                    }
                }
            }

            // 如果模型未加载，无法判断异常
            if (!modelsLoaded)
                return false;

            var loc = ev.Location;

            // 解析 COG（方向）和 VesselType（vtype）
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
                Console.WriteLine($"PositionAnomalyCondition: H3 检测调用失败: {ex.Message}");
                // 模型错误按安全策略返回 false
            }

            return false;
        }
    }
}