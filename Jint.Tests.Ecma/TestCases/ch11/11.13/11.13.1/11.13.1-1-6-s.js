/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * PutValue operates only on references (see step 3.a).
 *
 * @path ch11/11.13/11.13.1/11.13.1-1-6-s.js
 * @description simple assignment throws ReferenceError if LeftHandSide is an unresolvable reference in strict mode (base obj undefined)
 * @onlyStrict
 */


function testcase() {
  'use strict';
  
  try {
    __ES3_1_test_suite_test_11_13_1_unique_id_0__.x = 42;
    return false;
  }
  catch (e) {
    return (e instanceof ReferenceError);
  }
 }
runTestCase(testcase);
