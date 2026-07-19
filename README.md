<p align="center">
  <img width="423" alt="Screenshot" src="https://github.com/user-attachments/assets/3518a84b-b58c-4c24-8e24-d1c0176471bc" />
</p>

This is an [ISF shader](https://isf.video) for perfect particle number
conservation with cellular automaton particle tracking. This shader is converted
from [this ShaderToy shader](https://www.shadertoy.com/view/3s3cWr) by
[**@MichaelMoroz**](https://github.com/MichaelMoroz).

This is a multi-pass shader that is intended to be used with floating-point
buffers. Not all ISF hosts support floating-point buffers.
[Videosync](https://videosync.showsync.com/download) supports floating-point
buffers in
[v2.0.12](https://support.showsync.com/release-notes/videosync/2.0#2012) and
later, but https://editor.isf.video does not appear to support floating-point
buffers. This shader will produce *very* different output if floating-point
buffers are not used.
