cmake_minimum_required(VERSION 3.16)
project(nng_native)

set(BUILD_SHARED_LIBS ON)
set(NNG_ELIDE_DEPRECATED ON)

add_subdirectory(nng)

# Force static CRT linking with MSVC
if (MSVC)
    set_target_properties(nng PROPERTIES MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
    target_compile_options(nng PRIVATE "$<$<CONFIG:Release>:/Zi>")
    target_link_options(nng PRIVATE "$<$<CONFIG:Release>:/DEBUG>")
    target_link_options(nng PRIVATE "$<$<CONFIG:Release>:/OPT:REF>")
    target_link_options(nng PRIVATE "$<$<CONFIG:Release>:/OPT:ICF>")
endif()
set_target_properties(nng PROPERTIES OUTPUT_NAME "nng_native")
