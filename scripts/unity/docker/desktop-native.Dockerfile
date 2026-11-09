# CPU toolchain + software-rendered Linux player verification. No hardware devices exposed.
FROM ubuntu:24.04@sha256:fe81560990fae86dc4743202b2cb4d3df98d0bcf949173cecef15ce1127ef852
RUN apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
    build-essential cmake gcc-mingw-w64-x86-64-posix python3 \
    libegl1 libgles2 libgl1-mesa-dri libasound2t64 libpulse0 \
    libxcursor1 libxrandr2 libxinerama1 libxi6 libxrender1 libnss3 xvfb xauth \
    && rm -rf /var/lib/apt/lists/*
ENV LIBGL_ALWAYS_SOFTWARE=1
