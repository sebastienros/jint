/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-5-1.js
 * @description Array.prototype.reduce throws TypeError if 'length' is 0 (empty array), no initVal
 */


function testcase() {
  function cb(){}
  
  try {
    [].reduce(cb);
  }
  catch (e) {
    if (e instanceof TypeError) {
      return true;
    }
  }
 }
runTestCase(testcase);
