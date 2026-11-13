cmake_minimum_required(VERSION 3.21)
project(timer_pulses C)
enable_testing()

# Static, because the test runner copies executables only, not the project's shared libraries.
add_library(ticker STATIC src/ticker.c)
target_include_directories(ticker PUBLIC src)

foreach(name ticks timeout bad_period)
  add_executable(test_${name} tests/test_${name}.c)
  target_link_libraries(test_${name} PRIVATE ticker)
  add_test(NAME ${name} COMMAND test_${name})
  set_tests_properties(${name} PROPERTIES TIMEOUT 10)
endforeach()
