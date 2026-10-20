BASE 0x140000000 SECTIONS [('.tls$', '0x1000', '0x400', '0x4042000'), ('.rdata', '0x4043000', '0x4042400', '0x133c000'), ('.xcode', '0x537f000', '0x537e400', '0x2bc600'), ('.sbss', '0x5c66000', '0x563aa00', '0x347000'), ('.arch', '0x5fad000', '0x5981a00', '0x1000'), ('.tls', '0x5fae000', '0x5982a00', '0x200'), ('.idata', '0x5faf000', '0x5982c00', '0x28000'), ('.xtext', '0x5fd7000', '0x59aac00', '0x56000'), ('.srdata', '0x602d000', '0x5a00c00', '0x197000'), ('.text', '0x61c4000', '0x5b97c00', '0xfe32200'), ('.udata', '0x15ff7000', '0x159c9e00', '0x33e00'), ('.xpdata', '0x1602b000', '0x159fdc00', '0x200'), ('.sdata', '0x1602c000', '0x159fde00', '0x2400'), ('.data2', '0x1602f000', '0x15a00200', '0x346400'), ('.v38', '0x16376000', '0x15d46600', '0x200')]
141328ea0  mov       qword ptr [rsp + 8], rbx
141328ea5  mov       qword ptr [rsp + 0x10], rsi
141328eaa  push      rdi
141328eab  sub       rsp, 0x30
141328eaf  mov       r8d, dword ptr [rcx + 0x7d4]
141328eb6  movaps    xmm0, xmm1
141328eb9  addss     xmm0, dword ptr [rcx + 0x7e0]
141328ec1  inc       r8d
141328ec4  movaps    xmmword ptr [rsp + 0x20], xmm6
141328ec9  movaps    xmm2, xmm1
141328ecc  mov       eax, 0x24924925
141328ed1  addss     xmm2, dword ptr [rcx + 0x7b8]
141328ed9  mul       r8d
141328edc  movss     dword ptr [rcx + 0x7e0], xmm0
141328ee4  movaps    xmm6, xmm1
141328ee7  movss     xmm0, dword ptr [rcx + 0x7d8]
