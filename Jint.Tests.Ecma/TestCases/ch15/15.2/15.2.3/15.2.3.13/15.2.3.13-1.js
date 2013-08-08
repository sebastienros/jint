/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-1.js
 * @description Object.isExtensible throws TypeError if type of first param is not Object
 */


function testcase() {
    try {
      Object.isExtensible(0);
    }
    catch (e) {
      if (e instanceof TypeError) {
        return true;
      }
    }
 }
runTestCase(testcase);
