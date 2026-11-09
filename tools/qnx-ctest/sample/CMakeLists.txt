cmake_minimum_required(VERSION 3.21)
project(qnx_ctest_sample C)
enable_testing()

add_executable(ipc_roundtrip ipc_roundtrip.c)
add_test(NAME ipc_roundtrip COMMAND ipc_roundtrip)

add_executable(args_exit args_exit.c)
add_test(NAME args_pass COMMAND args_exit "two words" 0)
add_test(NAME macro_arg COMMAND args_exit __LINE__ 0)

add_test(NAME will_fail COMMAND args_exit x 1)
set_tests_properties(will_fail PROPERTIES WILL_FAIL TRUE)

add_test(NAME skipped COMMAND args_exit x 77)
set_tests_properties(skipped PROPERTIES SKIP_RETURN_CODE 77)

add_test(NAME disabled COMMAND args_exit x 9)
set_tests_properties(disabled PROPERTIES DISABLED TRUE)

add_executable(env_check env_check.c)
add_test(NAME env_check COMMAND env_check)
set_tests_properties(env_check PROPERTIES ENVIRONMENT "QCT_VALUE=a b")

option(ONLY_PASSING "Leave out the tests that are meant to fail" OFF)
if(NOT ONLY_PASSING)
  add_test(NAME args_fail COMMAND args_exit "two words" 7)
  add_test(NAME exit_137 COMMAND args_exit x 137)
  add_executable(crash crash.c)
  add_test(NAME crash_will_fail COMMAND crash)
  set_tests_properties(crash_will_fail PROPERTIES WILL_FAIL TRUE)
  add_executable(hang hang.c)
  add_test(NAME hang COMMAND hang)
  set_tests_properties(hang PROPERTIES TIMEOUT 3)
endif()
