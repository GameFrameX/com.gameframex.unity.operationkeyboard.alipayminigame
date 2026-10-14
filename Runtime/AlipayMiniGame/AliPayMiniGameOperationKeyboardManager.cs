#if UNITY_WEBGL && ENABLE_ALIPAY_MINI_GAME && ENABLE_ALIPAY_MINI_GAME_OPERATIONKEYBOARD

// ==========================================================================================
//   GameFrameX 组织及其衍生项目的版权、商标、专利及其他相关权利
//   GameFrameX organization and its derivative projects' copyrights, trademarks, patents, and related rights
//   均受中华人民共和国及相关国际法律法规保护。
//   are protected by the laws of the People's Republic of China and relevant international regulations.
//   使用本项目须严格遵守相关法律法规及开源许可证之规定。
//   Usage of this project must strictly comply with applicable laws, regulations, and open-source licenses.
//   本项目采用 MIT 许可证与 Apache License 2.0 双许可证分发，
//   This project is dual-licensed under the MIT License and Apache License 2.0,
//   完整许可证文本请参见源代码根目录下的 LICENSE 文件。
//   please refer to the LICENSE file in the root of the source tree for the full license text.
//   禁止利用本项目实施任何危害国家安全、破坏社会秩序、
//   It is prohibited to use this project to engage in any activities that endanger national security, disrupt social order,
//   侵犯他人合法权益等法律法规所禁止的行为！
//   or infringe upon the legitimate rights and interests of others, as prohibited by laws and regulations!
//   因基于本项目二次开发所产生的一切法律纠纷与责任，
//   Any disputes or liabilities arising from secondary development based on this project
//   本项目组织及贡献者概不承担。
//   shall be borne solely by the developer; the project organization and contributors assume no responsibility.
//   GitHub 仓库：https://github.com/GameFrameX
//   GitHub Repository: https://github.com/GameFrameX
//   Gitee  仓库：https://gitee.com/GameFrameX
//   Gitee Repository:  https://gitee.com/GameFrameX
//   CNB  仓库：https://cnb.cool/GameFrameX
//   CNB Repository:    https://cnb.cool/GameFrameX
//   官方文档：https://gameframex.doc.alianblank.com/
//   Official Documentation: https://gameframex.doc.alianblank.com/
//  ==========================================================================================

using System;
using AlipaySdk;
using GameFrameX.OperationKeyboard.Runtime;
using UnityEngine;

namespace GameFrameX.OperationKeyboard.AlipayMiniGame.Runtime
{
    /// <summary>
    /// 支付宝小游戏键盘管理器。
    /// </summary>
    /// <remarks>
    /// 直通平台键盘 API（AP 的 showKeyboard 家族，事件回调为含 value 字段的 JSON 字符串）；
    /// Hide 时注销全部平台事件；事件单槽回调由 OperationKeyboard 门面在字段会话内统一登记。
    /// This manager drives the AP showKeyboard family directly (event payloads are JSON strings with a value field),
    /// unregisters every platform event on Hide, and relies on the facade to register single-slot callbacks per field session.
    /// </remarks>
    [UnityEngine.Scripting.Preserve]
    public sealed class AliPayMiniGameOperationKeyboardManager : IOperationKeyboardManager
    {
        /// <summary>
        /// 渠道唯一标识
        /// </summary>
        public const string ChannelId = "alipayminigame";

        /// <summary>
        /// 支付宝键盘事件回调 JSON 包装：{"value":...}（输入 / 确认 / 完成同构）。
        /// </summary>
        [Serializable]
        private class ApKeyboardEvent
        {
            /// <summary>事件携带的输入内容</summary>
            public string value;
        }

        private Action<string> _onInput;
        private Action<string> _onConfirm;
        private Action<string> _onComplete;
        private Action<float> _onHeightChanged;

        private Action<string> _onKeyboardInputListener;
        private Action<string> _onKeyboardConfirmListener;
        private Action<string> _onKeyboardCompleteListener;

        private bool _listening;

        /// <summary>
        /// 拉起键盘并订阅平台键盘事件。
        /// </summary>
        /// <param name="option">键盘参数；<see cref="OperationKeyboardShowOption.MaxLength"/> 恒为 -1（不限长）。</param>
        /// <remarks>
        /// 支付宝 maxLength 方言以 0 表示不限长，契约的 -1 在此处内部映射为 0，不对调用方暴露；
        /// ShowKeyboard 的结果回调为 JSON 字符串，本契约无对应交付面，忽略内容即可。
        /// Alipay treats maxLength 0 as unlimited, so the contract's -1 is mapped to 0 here;
        /// the show result callback JSON is ignored as the contract has no delivery slot for it.
        /// </remarks>
        [UnityEngine.Scripting.Preserve]
        public void Show(OperationKeyboardShowOption option)
        {
            if (option == null)
            {
                return;
            }

            EnsureListeners();

            AP.ShowKeyboard(option.DefaultValue ?? string.Empty, option.MaxLength < 0 ? 0 : option.MaxLength, option.Multiple, option.ConfirmHold, option.ConfirmType, json => { });
        }

        /// <summary>
        /// 收起键盘并注销全部平台事件。
        /// </summary>
        [UnityEngine.Scripting.Preserve]
        public void Hide()
        {
            if (!_listening)
            {
                return;
            }

            AP.HideKeyboard(json => { });

            AP.OffKeyboardInput(_onKeyboardInputListener);
            AP.OffKeyboardConfirm(_onKeyboardConfirmListener);
            AP.OffKeyboardComplete(_onKeyboardCompleteListener);

            _onInput = null;
            _onConfirm = null;
            _onComplete = null;
            _onHeightChanged = null;
            _listening = false;
        }

        /// <summary>
        /// 更新键盘输入框内容（仅键盘处于拉起状态时有效）。
        /// </summary>
        /// <param name="value">当前输入框内容。</param>
        /// <remarks>
        /// 支付宝无对应 API，按契约空实现（核心契约不因单平台缺席而缩面）。
        /// Alipay exposes no updateKeyboard API, so this is a no-op per contract.
        /// </remarks>
        [UnityEngine.Scripting.Preserve]
        public void UpdateValue(string value)
        {
        }

        /// <summary>
        /// 登记键盘输入事件回调（单槽，后设覆盖先设）。
        /// </summary>
        /// <param name="handler">回调：键盘输入的当前值。</param>
        [UnityEngine.Scripting.Preserve]
        public void OnInput(Action<string> handler)
        {
            _onInput = handler;
        }

        /// <summary>
        /// 登记键盘确认事件回调（单槽，后设覆盖先设）。
        /// </summary>
        /// <param name="handler">回调：确认时的当前值。</param>
        [UnityEngine.Scripting.Preserve]
        public void OnConfirm(Action<string> handler)
        {
            _onConfirm = handler;
        }

        /// <summary>
        /// 登记键盘完成（收起）事件回调（单槽，后设覆盖先设）。
        /// </summary>
        /// <param name="handler">回调：完成时的终值。</param>
        [UnityEngine.Scripting.Preserve]
        public void OnComplete(Action<string> handler)
        {
            _onComplete = handler;
        }

        /// <summary>
        /// 登记键盘高度变化事件回调（供避让布局消费）。
        /// </summary>
        /// <param name="handler">回调：键盘高度（像素，0 表示收起）。</param>
        /// <remarks>
        /// 支付宝无键盘高度 API，按契约空实现（核心契约不因单平台缺席而缩面）：
        /// 仅保留单槽登记，无平台事件可订阅，该槽位不会被触发。
        /// Alipay exposes no keyboard-height API; the slot is kept for contract symmetry but never fires.
        /// </remarks>
        [UnityEngine.Scripting.Preserve]
        public void OnHeightChanged(Action<float> handler)
        {
            _onHeightChanged = handler;
        }

        private void EnsureListeners()
        {
            if (_listening)
            {
                return;
            }

            _onKeyboardInputListener = json => Forward(json, _onInput);
            _onKeyboardConfirmListener = json => Forward(json, _onConfirm);
            _onKeyboardCompleteListener = json => Forward(json, _onComplete);

            AP.OnKeyboardInput(_onKeyboardInputListener);
            AP.OnKeyboardConfirm(_onKeyboardConfirmListener);
            AP.OnKeyboardComplete(_onKeyboardCompleteListener);

            _listening = true;
        }

        private static void Forward(string json, Action<string> handler)
        {
            if (handler == null)
            {
                return;
            }

            try
            {
                var keyboardEvent = JsonUtility.FromJson<ApKeyboardEvent>(string.IsNullOrEmpty(json) ? "{}" : json);
                if (keyboardEvent != null)
                {
                    handler.Invoke(keyboardEvent.value ?? string.Empty);
                }
            }
            catch (Exception)
            {
                // 解析失败按无效载荷丢弃，不向 SDK 回调抛出（与粘贴板渠道同策略）
            }
        }
    }
}

#endif
