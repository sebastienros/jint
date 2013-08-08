/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-b-1.js
 * @description Array.prototype.reduce returns initialvalue when Array is empty and initialValue is present
 */


function testcase() { 
 
  function callbackfn(prevVal, curVal, idx, obj)
  {
  }

  var arr = new Array(10);

  if(arr.reduce(callbackfn,5) === 5)
      return true;  

 }
runTestCase(testcase);
