/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-4-3.js
 * @description Array.prototype.some throws TypeError if callbackfn is null
 */


function testcase() {

  var arr = new Array(10);
  try {
    arr.some(null);    
  }
  catch(e) {
    if(e instanceof TypeError)
      return true;  
  }

 }
runTestCase(testcase);
