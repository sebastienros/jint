/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-1-5.js
 * @description Object.keys throws TypeError if type of first param is not Object (undefined)
 */


function testcase() {
  try {
    Object.keys(undefined);
  }
  catch (e) {
    if (e instanceof TypeError) {
      return true;
    }
  }
 }
runTestCase(testcase);
