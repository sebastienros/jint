/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-4-6.js
 * @description Array.prototype.map throws TypeError if callbackfn is string
 */


function testcase() {

  var arr = new Array(10);
  try {
    arr.map("abc");    
  }
  catch(e) {
    if(e instanceof TypeError)
      return true;  
  }

 }
runTestCase(testcase);
