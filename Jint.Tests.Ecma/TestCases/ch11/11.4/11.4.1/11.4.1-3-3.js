/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.4/11.4.1/11.4.1-3-3.js
 * @description delete operator returns true when deleting an explicitly qualified yet unresolvable reference (property undefined for base obj)
 */


function testcase() {
  var __ES3_1_test_suite_test_11_4_1_3_unique_id_3__ = {};
  var d = delete __ES3_1_test_suite_test_11_4_1_3_unique_id_3__.x;
  if (d === true) {
    return true;
  }
 }
runTestCase(testcase);
