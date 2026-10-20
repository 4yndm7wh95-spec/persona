BASE 0x140000000 SECTIONS [('.tls$', '0x1000', '0x400', '0x4042000'), ('.rdata', '0x4043000', '0x4042400', '0x133c000'), ('.xcode', '0x537f000', '0x537e400', '0x2bc600'), ('.sbss', '0x5c66000', '0x563aa00', '0x347000'), ('.arch', '0x5fad000', '0x5981a00', '0x1000'), ('.tls', '0x5fae000', '0x5982a00', '0x200'), ('.idata', '0x5faf000', '0x5982c00', '0x28000'), ('.xtext', '0x5fd7000', '0x59aac00', '0x56000'), ('.srdata', '0x602d000', '0x5a00c00', '0x197000'), ('.text', '0x61c4000', '0x5b97c00', '0xfe32200'), ('.udata', '0x15ff7000', '0x159c9e00', '0x33e00'), ('.xpdata', '0x1602b000', '0x159fdc00', '0x200'), ('.sdata', '0x1602c000', '0x159fde00', '0x2400'), ('.data2', '0x1602f000', '0x15a00200', '0x346400'), ('.v38', '0x16376000', '0x15d46600', '0x200')]
14162f080  sub       rsp, 0x38
14162f084  mov       rax, qword ptr [rip + 0x4093ff5]
14162f08b  test      rax, rax
14162f08e  jne       0x14162f0cd
14162f090  mov       qword ptr [rsp + 0x30], rbx
14162f095  call      0x14162eb30
14162f09a  mov       ebx, eax
14162f09c  call      0x141700f50
14162f0a1  mov       rdx, rax
14162f0a4  mov       dword ptr [rsp + 0x20], ebx
14162f0a8  mov       r9d, 0x48
14162f0ae  lea       r8, [rip + 0x2de5eb3] ; wide='CampParamTableTopRow'
14162f0b5  lea       rcx, [rip + 0x2c4] ; mem=0x14162f380 f32=-8.73727913131006e-05
14162f0bc  call      0x141a3c250
14162f0c1  mov       rbx, qword ptr [rsp + 0x30]
14162f0c6  mov       qword ptr [rip + 0x4093fb3], rax
14162f0cd  add       rsp, 0x38
14162f0d1  ret       
