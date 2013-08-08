/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-1.js
 * @description Array.prototype.some considers new elements added to array after it is called
 */


function testcase() { 
  var calledForThree = false;
 
  function callbackfn(val, idx, obj)
  {
    arr[2] = 3;
    if(val !== 3)
      calledForThree = true;

    return false;
  }

  var arr = [1,2,,4,5];
  
  var val = arr.some(callbackfn);
  return calledForThree;
 }
runTestCase(testcase);
