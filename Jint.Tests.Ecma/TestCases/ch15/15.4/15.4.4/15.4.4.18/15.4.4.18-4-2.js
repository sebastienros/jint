/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-4-2.js
 * @description Array.prototype.forEach throws ReferenceError if callbackfn is unreferenced
 */


function testcase() {

  var arr = new Array(10);
  try {
    arr.forEach(foo);    
  }
  catch(e) {
    if(e instanceof ReferenceError)
      return true;  
  }

 }
runTestCase(testcase);
