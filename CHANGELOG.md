# Changelog

## [0.2.1](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/compare/v0.2.0...v0.2.1) (2026-03-27)


### Bug Fixes

* add netstandard polyfills for IsExternalInit, RequiredMemberAttribute, CompilerFeatureRequiredAttribute ([806143f](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/commit/806143f171bc896a982930744ac50ea09a8e9f60))

## [0.2.0](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/compare/v0.1.0...v0.2.0) (2026-03-27)


### Features

* add async event args classes ([94ffaac](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/commit/94ffaacfaa976a794377dcf318cb60ccb1321b24))
* add AsyncEvent delegates and InvokeMode enum ([dd25a46](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/commit/dd25a46067efc2dc0f64cb1f6aee93023e6ca1ec))
* add AsyncEventHandler&lt;T&gt; struct with lock-free register/unregister ([8c0e7e4](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/commit/8c0e7e4e4018365da2cb11dafd2153b67db12967))
* add CancelableAsyncEventHandler with sequential cancel short-circuit ([9b3195f](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/commit/9b3195f18380894d5f8e98ce278209f65395a0d4))
* add IAsyncEventArgs, ICancelable, SourcedAsyncEventArgs, CancelEventArgs ([2b28b4d](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/commit/2b28b4d685948bfd3d638964d517989ab793c43e))
* add INotifyPropertyChangedAsync and related interfaces ([5a9b38d](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/commit/5a9b38de67d23093fa2babe545498219b976d798))
* add InvokeAsync overload with mode override for field-level InvokeSequentially ([4f86f46](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/commit/4f86f464f080b90d48706fb853b93f41df683fa2))
* add InvokeAsync with parallel (ArrayPool) and sequential modes ([adb1132](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/commit/adb11320ac3d0d517773d8d41fdcc2a355994e0f))
* multi-target netstandard2.0/2.1, net8.0, net10.0 with AOT compatibility ([b49a466](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/commit/b49a466126131ff08a77e8add8f1f3a8e60c793b))


### Bug Fixes

* target netstandard2.0 with System.Threading.Tasks.Extensions for ValueTask ([67b943c](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/commit/67b943c3b5a2d72d030b7c0a0682228951246868))


### Performance Improvements

* add AsyncEventHandler benchmarks ([c404e6c](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/commit/c404e6cfb394e6dcb1908ab7d2aa17cc0baf3778))
* add CancelableAsyncEventHandler benchmarks ([c5b9fe3](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/commit/c5b9fe391f76b51370af79cf226cdd83477c026b))
* add framework comparison benchmarks (sync delegate, naive async, ZeroAlloc) ([f458509](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/commit/f458509747c1b55e071d3c02b6059af705c06170))
* use ReadOnlySpan&lt;Task&gt; WhenAll overload on net10.0, document LTS-only TFM policy ([6664744](https://github.com/ZeroAlloc-Net/ZeroAlloc.AsyncEvents/commit/6664744d6500c674cc004ec5847977c0bcc748f8))
