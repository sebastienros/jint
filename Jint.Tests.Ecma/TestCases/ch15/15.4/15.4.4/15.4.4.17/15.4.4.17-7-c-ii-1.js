/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-1.js
 * @description Array.prototype.some - callbackfn called with correct parameters
 */


function testcase() { 
 
  function callbackfn(val, idx, obj)
  {
    if(obj[idx] === val)
      return false;
    else
      return true;
  }

  var arr = [0,1,2,3,4,5,6,7,8,9];
  
  if(arr.some(callbackfn) === false)
    return true;


 }
runTestCase(testcase);
