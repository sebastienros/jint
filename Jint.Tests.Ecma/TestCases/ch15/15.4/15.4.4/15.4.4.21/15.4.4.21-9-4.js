/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-4.js
 * @description Array.prototype.reduce doesn't visit deleted elements when Array.length is decreased
 */


function testcase() { 
 
  function callbackfn(prevVal, curVal, idx, obj)
  {
    arr.length = 2;
    return prevVal + curVal;
  }

  var arr = [1,2,3,4,5];
  if(arr.reduce(callbackfn) === 3 )
    return true;  
  
 }
runTestCase(testcase);
