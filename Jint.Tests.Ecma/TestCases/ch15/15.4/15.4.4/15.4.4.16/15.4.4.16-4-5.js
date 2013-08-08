/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-4-5.js
 * @description Array.prototype.every throws TypeError if callbackfn is number
 */


function testcase() {

  var arr = new Array(10);
  try {
    arr.every(5);    
  }
  catch(e) {
    if(e instanceof TypeError)
      return true;  
  }

 }
runTestCase(testcase);
