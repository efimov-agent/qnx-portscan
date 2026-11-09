# Headless QNX SDP 8.0 + QEMU on Ubuntu 24.04

Verified 2026-11-09 with Software Center 2.0.4 and baseline `com.qnx.qnx800`.
The SDP is licensed software: it is not part of this repository.

## Install
1. In the myQNX License Manager: accept the license, then deploy it to your user.
2. `./qnx-setup-*-linux.run --nox11 -- -f -y -d ~/qnx/qnxsoftwarecenter` (`-y` accepts the EULA).
3. `CLT=~/qnx/qnxsoftwarecenter/qnxsoftwarecenter/qnxsoftwarecenter_clt`
4. `-syncLicenseKeys` may list nothing. If `-installBaseline` then reports a key that is
   not activated, add that key with `$CLT -myqnx.user U -myqnx.password P -addLicenseKey KEY`.
5. `$CLT -myqnx.user U -myqnx.password P -mirror -listQuery com.qnx.qnx800`
6. `$CLT -myqnx.user U -myqnx.password P -cleanInstall -destination ~/qnx800 -installBaseline com.qnx.qnx800`
   (about 5 minutes, 3.5 GB).

## Build and boot
```sh
. ~/qnx800/qnxsdp-env.sh
qcc -Vgcc_ntox86_64 hello.c -o hello
mkdir -p ~/qnx-vm && cd ~/qnx-vm
mkqnximage --type=qemu --arch=x86_64 --noprompt --build   # creates local/ and output/
```
The stock `mkqnximage --run` uses a host network bridge. To leave host networking alone, boot directly:
```sh
cd ~/qnx-vm/output
qemu-system-x86_64 -enable-kvm -cpu host -m 1G -smp 2 -display none \
  -serial file:serial.log -daemonize -pidfile qemu.pid \
  -drive file=disk-qemu,if=ide,format=raw \
  -netdev user,id=n0,restrict=on,hostfwd=tcp:127.0.0.1:2222-:22 \
  -device virtio-net-pci,netdev=n0
```
The user must be in the `kvm` group (or use `sg kvm -c ...`).

## Running a program in the guest
Add the file to the image and run it from the startup snippet; output goes to `serial.log`.
- `local/snippets/data_files.custom`: `[uid=0 gid=0 perms=0755] /data/hello = /abs/path/hello`
- `local/snippets/post_start.custom`: `/data/data/hello`

Paths in `data_files.custom` are relative to the data partition mounted at `/data`,
so `/data/hello` appears in the guest as `/data/data/hello`.
Rebuild with `mkqnximage --type=qemu --arch=x86_64 --noprompt --force --build`.

## Running a CTest suite in the guest
`tools/qnx-ctest/qnx_ctest.py` runs the tests of a qcc-built CMake project in a throwaway guest:
```sh
. ~/qnx800/qnxsdp-env.sh
cmake -S . -B build -DCMAKE_TOOLCHAIN_FILE=$PWD/tools/qnx-ctest/qnx-x86_64.cmake
cmake --build build
python3 tools/qnx-ctest/qnx_ctest.py build --junit out/junit.xml
```
It reads the test list from `ctest --show-only=json-v1`, bakes the executables into an image under
`~/.cache/qnx-ctest`, boots it, and parses per-test markers from the serial log. The exit code is 0 only
if every test passed. A test past its CTest `TIMEOUT` (default `--timeout 60`) is killed and reported as a timeout.

CTest properties `DISABLED`, `WILL_FAIL`, `SKIP_RETURN_CODE` and `ENVIRONMENT` are honoured. Properties that decide
an outcome but cannot be applied (`PASS/FAIL/SKIP_REGULAR_EXPRESSION`, `ENVIRONMENT_MODIFICATION`, `TIMEOUT_AFTER_MATCH`)
stop the run with an error.

`--files DIR` places the contents of `DIR` in the directory the tests run in. `--user NAME` runs them as that
guest user (the image has `qnxuser`) and hands the directory over to it; the default is root.

Limits: shared libraries built by the project, host paths in arguments and `WORKING_DIRECTORY`
are not carried over; link tests statically or use system libraries. See `docs/libuv-on-qnx.md` for a full example.
The timeout kills only the test process, not processes it spawned. Tests run one by one, in CTest order; `DEPENDS`, `FIXTURES_*` and `REQUIRED_FILES` are ignored, so a test
whose setup failed still runs. An exit status above 128 counts as a crash and always fails.
`tools/qnx-ctest/check.sh` is the end-to-end check on a sample suite (needs the SDP and KVM).

## Known issue
The generated image prints `/usr/libexec/sshd-auth does not exist or is not executable`
and sshd does not start, so file transfer over SSH is not available; the runner above does not need it.
