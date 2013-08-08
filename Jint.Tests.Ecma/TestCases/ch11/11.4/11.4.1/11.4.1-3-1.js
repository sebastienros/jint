/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.4/11.4.1/11.4.1-3-1.js
 * @description delete operator returns true when deleting an unresolvable reference
 */


function testcase() {
  // just cooking up a long/veryLikely unique name
  var d = delete __ES3_1_test_suite_test_11_4_1_3_unique_id_0__;
  if (d === true) {
    return true;
  }
 }
runTestCase(testcase);
