import SwiftUI

struct PermissionOnboardingView: View {
    let onRequestPermission: () -> Void
    let onRecheckPermission: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 22) {
            VStack(alignment: .leading, spacing: 5) {
                Text("欢迎使用 MirrorCast")
                    .font(.system(size: 23, weight: .bold))
                Text("开始前，需要先允许屏幕录制")
                    .foregroundStyle(.secondary)
            }

            HStack(alignment: .top, spacing: 14) {
                Image(systemName: "lock.shield")
                    .font(.system(size: 25))
                    .foregroundStyle(.tint)
                    .frame(width: 32)

                VStack(alignment: .leading, spacing: 6) {
                    Text("为什么需要这个权限？")
                        .font(.headline)
                    Text("macOS 只有在获得屏幕录制权限后，才能读取你选择的窗口画面。MirrorCast 不会录制或保存画面。")
                        .font(.callout)
                        .foregroundStyle(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                }
            }

            Text("授权完成后，请完全退出并重新打开 MirrorCast。重启后会自动出现操作流程教学。")
                .font(.callout)
                .foregroundStyle(.secondary)
                .fixedSize(horizontal: false, vertical: true)

            Spacer(minLength: 0)

            VStack(spacing: 9) {
                Button("前往授权", action: onRequestPermission)
                    .frame(maxWidth: .infinity)
                    .controlSize(.large)
                    .buttonStyle(.borderedProminent)

                Button("我已授权，重新检测", action: onRecheckPermission)
                    .frame(maxWidth: .infinity)
                    .controlSize(.large)
            }
        }
        .padding(22)
    }
}
