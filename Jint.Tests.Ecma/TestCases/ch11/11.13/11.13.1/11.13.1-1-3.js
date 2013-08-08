/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * PutValue operates only on references (see step 1).
 *
 * @path ch11/11.13/11.13.1/11.13.1-1-3.js
 * @description simple assignment throws ReferenceError if LeftHandSide is not a reference (boolean)
 */


function testcase() {
  try {
    eval("true = 42");
  }
  catch (e) {
    if (e instanceof ReferenceError) {
      return true;
    }
  }
 }
runTestCase(testcase);
