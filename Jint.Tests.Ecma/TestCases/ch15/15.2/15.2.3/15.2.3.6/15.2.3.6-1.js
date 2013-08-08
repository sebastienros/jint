/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-1.js
 * @description Object.defineProperty throws TypeError if type of first param is not Object
 */


function testcase() {
    try {
      Object.defineProperty(true, "foo", {});
    }
    catch (e) {
      if (e instanceof TypeError) {
        return true;
      }
    }
 }
runTestCase(testcase);
