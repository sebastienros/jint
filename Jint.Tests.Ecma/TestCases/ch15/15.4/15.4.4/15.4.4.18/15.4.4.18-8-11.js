/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-8-11.js
 * @description Array.prototype.forEach doesn't mutate the array on which it is called on
 */


function testcase() {

  function callbackfn(val, idx, obj)
  {
    return true;
  }
  var arr = [1,2,3,4,5];
  arr.forEach(callbackfn);
  if(arr[0] === 1 &&
     arr[1] === 2 &&
     arr[2] === 3 &&
     arr[3] === 4 &&
     arr[4] === 5)
  {
    return true;
  }

 }
runTestCase(testcase);
