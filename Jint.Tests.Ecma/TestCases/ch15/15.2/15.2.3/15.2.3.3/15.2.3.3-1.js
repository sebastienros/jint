/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-1.js
 * @description Object.getOwnPropertyDescriptor throws TypeError if type of first param is not Object
 */


function testcase() {
  try {
    Object.getOwnPropertyDescriptor(0, "foo");
  }
  catch (e) {
    if (e instanceof TypeError) {
      return true;
    }
  }
 }
runTestCase(testcase);
