/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-5.js
 * @description Array.prototype.every doesn't consider newly added elements in sparse array
 */


function testcase() { 
 
  function callbackfn(val, Idx, obj)
  {
    arr[1000] = 3;
    if(val < 3)
       return true;
    else 
       return false;
  }

  var arr = new Array(10);
  arr[1] = 1;
  arr[2] = 2;
  
  if(arr.every(callbackfn) === true)    
      return true;  
  
 }
runTestCase(testcase);
