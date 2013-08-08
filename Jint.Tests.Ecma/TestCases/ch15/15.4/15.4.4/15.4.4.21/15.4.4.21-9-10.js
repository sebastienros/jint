/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-10.js
 * @description Array.prototype.reduce called with an initial value doesn't consider new elements added to array after it is called
 */


function testcase() { 
 
  function callbackfn(prevVal, curVal, idx, obj) {
    arr[5] = 6;
    arr[2] = 3;   
    return prevVal + curVal;
  }

  var arr = [1,2,,4,'5'];
  return arr.reduce(callbackfn, "") === "12345";
 }
runTestCase(testcase);
