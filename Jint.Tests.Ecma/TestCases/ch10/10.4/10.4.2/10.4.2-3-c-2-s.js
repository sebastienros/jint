/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.2/10.4.2-3-c-2-s.js
 * @description Calling code in strict mode - eval cannot instantiate variable in the variable environment of the calling context
 * @onlyStrict
 */


function testcase() {
  var _10_4_2_3_c_2_s = 0;
  function _10_4_2_3_c_2_sFunc() {
     'use strict';
     eval("var _10_4_2_3_c_2_s = 1");
     return _10_4_2_3_c_2_s===0;
  }
  return _10_4_2_3_c_2_sFunc();
 }
runTestCase(testcase);
