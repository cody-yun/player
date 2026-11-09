# 음성 입력 포팅과 검증

`src/runtime/voice.ts`의 `VoiceMeter.read`는 공용 C# `runtime/voice.cs`에 대응한다.
WebAudio 경계는 `Platform/Audio/PcmVoiceInput.cs`로 분리했고, Unity 마이크의 장치 경계는 `Platform/Unity/MicrophoneInput.cs`다.
`src/voice.ts`의 mouth driver와 lip-sync 제어는 `Runtime/voice.cs`, `src/runtime/talk.ts`의 말하기 동작은 `Runtime/runtime/talk.cs`다.
문장 음성 재생의 공통 큐/계산 코어와 아직 남은 출력 어댑터는 [SPEECH.md](SPEECH.md)에 따로 기록했다.

## 분석 계약

- 모노 입력 또는 stereo를 0.5 × (L + R)로 합친 입력.
- 650 Hz, Q 0.55 bandpass. 필터 state는 double, time samples는 float32.
- 가장 최근 1,024개 샘플의 RMS로 원본 noise floor/peak/gain/attack-release 계산.
- 필터 전 입력의 Blackman window + 1/N FFT, 512 bins, smoothing=0.
- Float32 dB → linear power Float32 경계를 거쳐 원본 VowelCue로 전달.
- 분석/입력 샘플레이트를 유지하며 `sampleRate / 1024`가 bin 간격이다.

공식 근거: [Web Audio FFT/windowing](https://www.w3.org/TR/webaudio/#fft-windowing-and-smoothing-over-time),
[Unity Microphone.Start](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/microphone/start),
[AudioClip.GetData](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/audioclip/getdata),
[RequestUserAuthorization](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/application/requestuserauthorization).

`capture-audio-reference.mjs`는 실제 Chrome OfflineAudioContext에 원본과 같은 BiquadFilter/Analyser 설정을 연결한다.
마이크·스피커를 열지 않는다. 16/44.1/48/96 kHz의 tone+noise 입력 8,192 samples, 전체 filter output,
마지막 time/raw window와 주파수 power를 `audio.json.gz`에 저장한다. 브라우저 버전과 원본 source hash를 함께 기록한다.
C# fixture 테스트는 filter/time 허용 오차 2e-7, power 1e-11 + 2e-5 × |reference|로 비교한다.
브라우저 FFT의 Float32 연산 오차를 허용하며 비트 단위 동등성을 주장하지 않는다.

`generate-drivers.mjs`는 원본 TalkMotion 5,400프레임과 공개 startFaceTracking/startLipSync 6,400프레임을 실행한다.
장치 시작만 stub 처리하며 원본 face poll/read/apply와 공개 driver를 실행한다. 얼굴 유실·새 결과 없음·mirror/calibration,
준비 중단·stop 반복·말하기와 침묵·tracked 상태에서 talk reset·motion/bounce/gain 설정을 포함한다.
Talk/driver 수치 허용 오차는 1e-9 × max(1, |reference|), RNG 호출 횟수와 ownership 해제 횟수는 정확히 비교한다.

## Unity 장치 경계와 남은 검증

`AudioCaptureInput`은 sample frame 기준의 순환 위치와 AudioClip.GetData로 읽는다. 버퍼보다 오래 멈췄으면
가장 최근 전체 버퍼만 처리한다. clip/release 소유권은 명시적으로 전달한다.
`MicrophoneInput.StartAsync`는 native capture를 시작하고 첫 샘플을 기다린다. 취소/시작 실패 시 자신이 시작한 capture와 clip을 해제한다.
같은 장치가 이미 녹음 중이면 거절하며 다른 capture를 중지하지 않는다.
권한 거부/Android dialog dismiss는 UnauthorizedAccessException, 취소는 OperationCanceledException이다.
이것은 원본의 모델 로드용 LivLivErrorCode에 새 코드를 추가하지 않는 platform 경계다.

Unity 테스트는 생성한 실제 AudioClip에서 mono/stereo 순환·wrap·청크 분할·동일 PCM 결과·중복 dispose·외부 clip 소유권을 검증한다.
이것은 실제 마이크 권한/장치 동작 검증이 아니다. PC/macOS privacy, Android/iOS 권한·background/resume·장치 제거,
WebGL 실제 getUserMedia 권한 및 실제 립싱크 지연/성능은 아직 남아 있다.
WebGL bridge 자체는 실제 WASM과 생성 MediaStream에서 확인했다. [WEBGL.md](WEBGL.md).
첫 ReadAt은 원본처럼 1/60초를 사용하며, 이후 monotonic ms 간격을 0.1초로 제한한다. 0 timestamp 반복·긴 gap을 원본 read()의 28개 결과로 비교한다.
