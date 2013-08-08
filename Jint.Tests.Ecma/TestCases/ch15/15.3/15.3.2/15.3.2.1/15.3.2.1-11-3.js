/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.2/15.3.2.1/15.3.2.1-11-3.js
 * @description Function constructor may have a formal parameter named 'eval' if body is not strict mode
 */


function testcase() {
  Function('eval', 'return;');
  return true;
  }
runTestCase(testcase);
