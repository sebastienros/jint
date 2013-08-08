/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-c-1.js
 * @description Array.prototype.reduceRight throws TypeError when Array is empty and initialValue is not present
 */


function testcase() { 
 
  function callbackfn(prevVal, curVal, idx, obj)
  {
  }

  var arr = new Array(10);
  try {
    arr.reduceRight(callbackfn);
  } 
  catch(e) {
    if(e instanceof TypeError)
      return true;  
  }
 }
runTestCase(testcase);
