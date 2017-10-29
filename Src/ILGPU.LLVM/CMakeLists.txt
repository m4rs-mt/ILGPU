cmake_minimum_required(VERSION 3.4.3)
project(ILGPU_LLVM)

# LLVM dependency
find_package(LLVM REQUIRED CONFIG)

message(STATUS "Found LLVM ${LLVM_PACKAGE_VERSION}")
message(STATUS "Using LLVMConfig.cmake in: ${LLVM_DIR}")

include_directories(${LLVM_INCLUDE_DIRS})
add_definitions(${LLVM_DEFINITIONS})

# ILGPU_LLVM
file(GLOB_RECURSE ILGPU_SOURCES "*.cpp")
file(GLOB_RECURSE ILGPU_HEADERS "*.h")

set (CMAKE_CXX_STANDARD 11)

if(WIN32)
    add_definitions(-DILGPULLVM_EXPORTS)
    add_library(ILGPU_LLVM STATIC ${ILGPU_SOURCES})
else()
    add_library(ILGPU_LLVM SHARED ${ILGPU_SOURCES})
endif()

llvm_map_components_to_libnames(llvm_libs all)

if(WIN32)
    target_link_libraries(ILGPU_LLVM ${llvm_libs})
elseif(APPLE)
    set_target_properties(ILGPU_LLVM PROPERTIES LINK_FLAGS "-Wl,-all_load")
    target_link_libraries(ILGPU_LLVM ${llvm_libs})
else()
    set_target_properties(ILGPU_LLVM PROPERTIES LINK_FLAGS "-Wl,--whole-archive")
    target_link_libraries(ILGPU_LLVM ${llvm_libs} "-Wl,--no-whole-archive")
endif()
