/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-4-7.js
 * @description Array.prototype.filter throws TypeError if callbackfn is Object without [[Call]] internal method
 */


function testcase() {

  var arr = new Array(10);
  try {
    arr.filter(new Object());    
  }
  catch(e) {
    if(e instanceof TypeError)
      return true;  
  }

 }
runTestCase(testcase);
