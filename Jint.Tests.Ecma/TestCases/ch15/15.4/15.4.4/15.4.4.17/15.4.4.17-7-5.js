/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-5.js
 * @description Array.prototype.some doesn't consider newly added elements in sparse array
 */


function testcase() { 
 
  function callbackfn(val, idx, obj)
  {
    arr[1000] = 5;
    if(val < 5)
      return false;
    else 
      return true;
  }

  var arr = new Array(10);
  arr[1] = 1;
  arr[2] = 2;
  
  if(arr.some(callbackfn) === false)    
    return true;  
 
 }
runTestCase(testcase);
