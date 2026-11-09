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

## Known issue
The generated image prints `/usr/libexec/sshd-auth does not exist or is not executable`
and sshd does not start, so file transfer over SSH is not available yet.
