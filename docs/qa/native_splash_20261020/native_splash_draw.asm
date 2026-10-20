BASE 0x140000000 SECTIONS [('.tls$', '0x1000', '0x400', '0x4042000'), ('.rdata', '0x4043000', '0x4042400', '0x133c000'), ('.xcode', '0x537f000', '0x537e400', '0x2bc600'), ('.sbss', '0x5c66000', '0x563aa00', '0x347000'), ('.arch', '0x5fad000', '0x5981a00', '0x1000'), ('.tls', '0x5fae000', '0x5982a00', '0x200'), ('.idata', '0x5faf000', '0x5982c00', '0x28000'), ('.xtext', '0x5fd7000', '0x59aac00', '0x56000'), ('.srdata', '0x602d000', '0x5a00c00', '0x197000'), ('.text', '0x61c4000', '0x5b97c00', '0xfe32200'), ('.udata', '0x15ff7000', '0x159c9e00', '0x33e00'), ('.xpdata', '0x1602b000', '0x159fdc00', '0x200'), ('.sdata', '0x1602c000', '0x159fde00', '0x2400'), ('.data2', '0x1602f000', '0x15a00200', '0x346400'), ('.v38', '0x16376000', '0x15d46600', '0x200')]
141316200  mov       qword ptr [rsp + 0x20], rbx
141316205  push      rbp
141316206  push      rsi
141316207  push      rdi
141316208  push      r12
14131620a  push      r13
14131620c  push      r14
14131620e  push      r15
141316210  lea       rbp, [rsp - 0x60]
141316215  sub       rsp, 0x160
14131621c  mov       rax, qword ptr [rip + 0x431c025] ; mem=0x145632248 f32=2.5424194000089884e-11
141316223  xor       rax, rsp
141316226  mov       qword ptr [rbp + 0x10], rax
14131622a  mov       edi, r9d
14131622d  mov       r13d, r8d
141316230  mov       r15, rdx
141316233  mov       r14, rcx
141316236  call      0x140fc85f0
14131623b  mov       dl, 0x32
14131623d  mov       rcx, rax
141316240  call      0x1415695e0
141316245  mov       rsi, rax
141316248  test      rax, rax
14131624b  je        0x141316270
14131624d  call      0x140ceb840
141316252  mov       rdx, qword ptr [rsi + 0x10]
141316256  lea       r8, [rax + 0x30]
14131625a  movsxd    rax, dword ptr [rax + 0x38]
14131625e  cmp       eax, dword ptr [rdx + 0x38]
141316261  jg        0x141316270
141316263  mov       rcx, rax
141316266  mov       rax, qword ptr [rdx + 0x30]
14131626a  cmp       qword ptr [rax + rcx*8], r8
14131626e  je        0x141316272
141316270  xor       esi, esi
141316272  call      0x14108f380
141316277  xor       r8d, r8d
14131627a  xor       edx, edx
14131627c  lea       r12, [rax + 0x20]
141316280  mov       rcx, r12
141316283  call      0x14135f830
141316288  xor       edx, edx
14131628a  mov       rcx, r12
14131628d  call      0x14135f4e0
141316292  mov       r9d, edi
141316295  mov       r8d, r13d
141316298  mov       rdx, r15
14131629b  mov       rcx, r14
14131629e  call      0x141313930
1413162a3  mov       ebx, dword ptr [rbp + 0xc0]
1413162a9  mov       r9d, r13d
1413162ac  movss     xmm2, dword ptr [rip + 0x2fee108] ; mem=0x1443043bc f32=396.0
1413162b4  mov       rcx, r14
1413162b7  movss     xmm1, dword ptr [rip + 0x3001ebd] ; mem=0x14431817c f32=904.0
1413162bf  mov       qword ptr [rsp + 0x30], r15
1413162c4  mov       dword ptr [rsp + 0x28], ebx
1413162c8  mov       dword ptr [rsp + 0x20], edi
1413162cc  call      0x14131fc70
1413162d1  movss     xmm2, dword ptr [rip + 0x3001ef3] ; mem=0x1443181cc f32=-138.0
1413162d9  mov       r9b, 0xff
1413162dc  movss     xmm1, dword ptr [rip + 0x2f48ca0] ; mem=0x14425ef84 f32=270.0
1413162e4  mov       rcx, r14
1413162e7  mov       qword ptr [rsp + 0x38], rsi
1413162ec  mov       dword ptr [rsp + 0x30], ebx
1413162f0  mov       dword ptr [rsp + 0x28], edi
1413162f4  mov       dword ptr [rsp + 0x20], r13d
1413162f9  call      0x141313570
1413162fe  movss     xmm2, dword ptr [rip + 0x2ff30aa] ; mem=0x1443093b0 f32=106.0
141316306  mov       r9b, 0xff
141316309  movss     xmm1, dword ptr [rip + 0x2ffecdf] ; mem=0x144314ff0 f32=296.0
141316311  mov       rcx, r14
141316314  mov       dword ptr [rsp + 0x28], ebx
141316318  mov       dword ptr [rsp + 0x20], edi
14131631c  call      0x141320fa0
141316321  mov       rcx, r15
141316324  call      0x1412e3bf0
141316329  movss     xmm0, dword ptr [r14 + 0x7e0]
141316332  mov       rbx, rax
141316335  mulss     xmm0, dword ptr [rip + 0x2d6b95b] ; mem=0x144081c98 f32=30.0
14131633d  call      0x141097fc0
141316342  cvttss2si edi, xmm0
141316346  cmp       edi, dword ptr [rbx + 0x20]
141316349  jge       0x14131672b
14131634f  movaps    xmmword ptr [rsp + 0x150], xmm6
141316357  movaps    xmmword ptr [rsp + 0x140], xmm7
14131635f  test      edi, edi
141316361  jne       0x14131642c
141316367  movzx     ebx, byte ptr [rip + 0x40a253a] ; wide='\x0b'
14131636e  lea       rcx, [r14 + 0x28]
141316372  mov       byte ptr [rsp + 0x38], bl
141316376  mov       r9b, 5
141316379  mov       byte ptr [rsp + 0x30], dil
14131637e  mov       r8b, 4
141316381  mov       byte ptr [rsp + 0x28], dil
141316386  xor       edx, edx
141316388  mov       byte ptr [rsp + 0x20], dil
14131638d  call      0x141376df0
141316392  movss     xmm6, dword ptr [rip + 0x2d6b8d6] ; mem=0x144081c70 f32=1.0
14131639a  lea       ecx, [rdi + 6]
14131639d  mov       dword ptr [rbp - 0x74], 0x3f800000
1413163a4  xorps     xmm7, xmm7
1413163a7  mov       dword ptr [rbp - 0x70], 0x3f800000
1413163ae  mov       qword ptr [rbp - 0x80], 0
1413163b6  call      0x1410b7f20
1413163bb  movss     xmm2, dword ptr [rip + 0x2e5725d] ; mem=0x14416d620 f32=1920.0
1413163c3  lea       rcx, [r14 + 0x28]
1413163c7  movss     xmm0, dword ptr [rip + 0x2e5724d] ; mem=0x14416d61c f32=1080.0
1413163cf  xorps     xmm3, xmm3
1413163d2  mov       byte ptr [rsp + 0x70], bl
1413163d6  xorps     xmm1, xmm1
1413163d9  mov       byte ptr [rsp + 0x68], dil
1413163de  mov       qword ptr [rsp + 0x60], rax
1413163e3  lea       rax, [rbp - 0x74]
1413163e7  mov       qword ptr [rsp + 0x58], rax
1413163ec  lea       rax, [rbp - 0x80]
1413163f0  mov       qword ptr [rsp + 0x50], rax
1413163f5  lea       rax, [rip + 0x3268e24] ; mem=0x14457f220 f32=nan
1413163fc  movss     dword ptr [rsp + 0x48], xmm7
141316402  movss     dword ptr [rsp + 0x40], xmm6
141316408  movss     dword ptr [rsp + 0x38], xmm6
14131640e  mov       qword ptr [rsp + 0x30], rax
141316413  movss     dword ptr [rsp + 0x28], xmm0
141316419  movss     dword ptr [rsp + 0x20], xmm2
14131641f  xorps     xmm2, xmm2
141316422  call      0x14136d360
141316427  jmp       0x14131671b
14131642c  movaps    xmmword ptr [rsp + 0x130], xmm8
141316435  movaps    xmmword ptr [rsp + 0x120], xmm9
14131643e  call      0x14108f380
141316443  movss     xmm9, dword ptr [rip + 0x2d6b824] ; mem=0x144081c70 f32=1.0
14131644c  xor       r8d, r8d
14131644f  movss     xmm7, dword ptr [rip + 0x2e31581] ; mem=0x1441479d8 f32=960.0
141316457  xorps     xmm6, xmm6
14131645a  movss     xmm8, dword ptr [rip + 0x2f46ced] ; mem=0x14425d150 f32=540.0
141316463  lea       rbx, [rax + 0x20]
141316467  mov       rcx, rbx
14131646a  cmp       edi, 0xa
14131646d  jge       0x141316576
141316473  lea       edx, [r8 + 1]
141316477  call      0x14135fc40
14131647c  mov       eax, dword ptr [rip + 0x40a2426] ; wide='\x0b'
141316482  xor       r13d, r13d
141316485  mov       dword ptr [rsp + 0x40], eax
141316489  xor       r8d, r8d
14131648c  mov       dword ptr [rsp + 0x38], 0xf
141316494  xor       edx, edx
141316496  mov       dword ptr [rsp + 0x30], r13d
14131649b  mov       rcx, rbx
14131649e  mov       dword ptr [rsp + 0x28], r13d
1413164a3  lea       r9d, [r13 + 1]
1413164a7  mov       dword ptr [rsp + 0x20], r13d
1413164ac  call      0x140ce34c0
1413164b1  mov       eax, r13d
1413164b4  mov       dword ptr [rbp - 0x78], r13d
1413164b8  mov       dword ptr [rbp - 0x2c], eax
1413164bb  movaps    xmm0, xmm7
1413164be  mov       eax, dword ptr [rip + 0x440fd6c]
1413164c4  mov       dword ptr [rbp - 0x40], eax
1413164c7  mov       eax, edi
1413164c9  cdq       
1413164ca  unpcklps  xmm0, xmm8
1413164ce  sub       eax, edx
1413164d0  movsd     qword ptr [rbp - 0x34], xmm0
1413164d5  movaps    xmm0, xmmword ptr [rip + 0x2d67ff4] ; wide=''
1413164dc  lea       edx, [r13 + 0x10]
1413164e0  sar       eax, 1
1413164e2  movaps    xmmword ptr [rbp - 0x60], xmm0
1413164e6  movsd     xmm0, qword ptr [rip + 0x440fd3a]
1413164ee  mov       qword ptr [rbp - 0x28], 0x3f800000
1413164f6  lea       ecx, [rax + 0xc]
1413164f9  mov       dword ptr [rbp - 0x50], 0x3f800000
141316500  mov       dword ptr [rbp - 0x4c], 0x3f800000
141316507  movsd     qword ptr [rbp - 0x48], xmm0
14131650c  mov       dword ptr [rbp - 0x3c], 0xffffffff
141316513  call      0x141097ee0
141316518  mov       dword ptr [rbp - 0x38], eax
14131651b  call      0x140fc85f0
141316520  mov       dl, 0x33
141316522  mov       rcx, rax
141316525  call      0x1415695e0
14131652a  mov       r15, rax
14131652d  test      rax, rax
141316530  je        0x14131656a
141316532  call      0x140d5b090
141316537  mov       rdx, qword ptr [r15 + 0x10]
14131653b  add       rax, 0x30
14131653f  movsxd    rcx, dword ptr [rax + 8]
141316543  cmp       ecx, dword ptr [rdx + 0x38]
141316546  jg        0x14131656a
141316548  mov       rdx, qword ptr [rdx + 0x30]
14131654c  cmp       qword ptr [rdx + rcx*8], rax
141316550  jne       0x14131656a
141316552  xorps     xmm3, xmm3
141316555  movss     dword ptr [rsp + 0x20], xmm6
14131655b  mov       r8, r15
14131655e  lea       rcx, [rbp - 0x60]
141316562  mov       rdx, rbx
141316565  call      0x141360190
14131656a  xor       edx, edx
14131656c  mov       rcx, rbx
14131656f  call      0x14135f4e0
141316574  jmp       0x141316580
141316576  xor       edx, edx
141316578  call      0x14135fc40
14131657d  xor       r13d, r13d
141316580  mov       edx, 6
141316585  mov       rcx, r12
141316588  call      0x14135f4e0
14131658d  mov       ecx, 6
141316592  mov       dword ptr [rbp - 0x80], 0x3f800000
141316599  mov       dword ptr [rbp - 0x7c], 0x3f800000
1413165a0  mov       qword ptr [rbp - 0x74], 0
1413165a8  call      0x1410b7f20
1413165ad  movss     xmm1, dword ptr [rip + 0x2e5706b] ; mem=0x14416d620 f32=1920.0
1413165b5  lea       rcx, [r14 + 0x28]
1413165b9  movzx     edx, byte ptr [rip + 0x40a22e8] ; wide='\x0b'
1413165c0  xorps     xmm3, xmm3
1413165c3  movss     xmm0, dword ptr [rip + 0x2e57051] ; mem=0x14416d61c f32=1080.0
1413165cb  xorps     xmm2, xmm2
1413165ce  mov       byte ptr [rsp + 0x70], dl
1413165d2  mov       byte ptr [rsp + 0x68], 0
1413165d7  mov       qword ptr [rsp + 0x60], rax
1413165dc  lea       rax, [rbp - 0x80]
1413165e0  mov       qword ptr [rsp + 0x58], rax
1413165e5  lea       rax, [rbp - 0x74]
1413165e9  mov       qword ptr [rsp + 0x50], rax
1413165ee  lea       rax, [rip + 0x3268c2b] ; mem=0x14457f220 f32=nan
1413165f5  movss     dword ptr [rsp + 0x48], xmm6
1413165fb  movss     dword ptr [rsp + 0x40], xmm9
141316602  movss     dword ptr [rsp + 0x38], xmm9
141316609  mov       qword ptr [rsp + 0x30], rax
14131660e  movss     dword ptr [rsp + 0x28], xmm0
141316614  movss     dword ptr [rsp + 0x20], xmm1
14131661a  xorps     xmm1, xmm1
14131661d  call      0x14136d360
141316622  xor       edx, edx
141316624  mov       rcx, r12
141316627  call      0x14135f4e0
14131662c  movaps    xmm9, xmmword ptr [rsp + 0x120]
141316635  movaps    xmm0, xmm7
141316638  unpcklps  xmm0, xmm8
14131663c  movaps    xmm8, xmmword ptr [rsp + 0x130]
141316645  movsd     qword ptr [rbp - 0x40], xmm0
14131664a  xorps     xmm0, xmm0
14131664d  mov       dword ptr [rbp - 0x78], 0
141316654  mov       eax, dword ptr [rbp - 0x78]
141316657  movdqa    xmmword ptr [rbp - 0x10], xmm0
14131665c  mov       dword ptr [rbp - 0x38], eax
14131665f  mov       qword ptr [rbp - 0x34], 0x3f800000
141316667  mov       qword ptr [rbp - 0x60], 0
14131666f  mov       dword ptr [rbp - 0x58], 0
141316676  mov       dword ptr [rbp - 0x4c], 0
14131667d  mov       dword ptr [rbp - 0x48], 0xffffffff
141316684  mov       qword ptr [rbp - 0x20], 0x3f800000
14131668c  mov       qword ptr [rbp - 0x2c], 0
141316694  mov       dword ptr [rbp - 0x24], 0x3f800000
14131669b  mov       dword ptr [rbp - 0x18], r13d
14131669f  mov       dword ptr [rbp - 0x44], 0x3f800000
1413166a6  mov       dword ptr [rbp], r13d
1413166aa  cmp       edi, 0xf
1413166ad  jge       0x1413166bd
1413166af  mov       eax, edi
1413166b1  cdq       
1413166b2  sub       eax, edx
1413166b4  sar       eax, 1
1413166b6  add       eax, 0x8c
1413166bb  jmp       0x1413166d3
1413166bd  lea       ecx, [rdi - 0xe]
1413166c0  mov       eax, 0x55555556
1413166c5  imul      ecx
1413166c7  mov       eax, edx
1413166c9  shr       eax, 0x1f
1413166cc  add       eax, 0x93
1413166d1  add       eax, edx
1413166d3  mov       edx, 0x8c
1413166d8  mov       dword ptr [rbp + 4], eax
1413166db  mov       ecx, eax
1413166dd  lea       r8d, [rdx + 9]
1413166e1  call      0x141093f70
1413166e6  movss     xmm0, dword ptr [rip + 0x2d838e2] ; mem=0x144099fd0 f32=2.0
1413166ee  lea       rcx, [rbp - 0x60]
1413166f2  movss     xmm1, dword ptr [rip + 0x30019de] ; mem=0x1443180d8 f32=1.13919997215271
1413166fa  xorps     xmm3, xmm3
1413166fd  mov       r8, rsi
141316700  movss     dword ptr [rbp - 0x54], xmm0
141316705  mov       rdx, r12
141316708  movss     dword ptr [rbp - 0x50], xmm1
14131670d  movss     dword ptr [rsp + 0x20], xmm6
141316713  mov       dword ptr [rbp + 4], eax
141316716  call      0x141362c50
14131671b  movaps    xmm6, xmmword ptr [rsp + 0x150]
141316723  movaps    xmm7, xmmword ptr [rsp + 0x140]
14131672b  mov       rcx, qword ptr [rbp + 0x10]
14131672f  xor       rcx, rsp
141316732  call      0x143f98a30
141316737  mov       rbx, qword ptr [rsp + 0x1b8]
14131673f  add       rsp, 0x160
141316746  pop       r15
141316748  pop       r14
14131674a  pop       r13
14131674c  pop       r12
14131674e  pop       rdi
14131674f  pop       rsi
141316750  pop       rbp
141316751  ret       
