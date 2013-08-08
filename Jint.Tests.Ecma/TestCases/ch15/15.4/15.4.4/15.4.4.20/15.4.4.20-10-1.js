/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-10-1.js
 * @description Array.prototype.filter doesn't mutate the Array on which it is called on
 */


function testcase() {

  function callbackfn(val, idx, obj)
  {
    return true;
  }
  var srcArr = [1,2,3,4,5];
  srcArr.filter(callbackfn);
  if(srcArr[0] === 1 &&
     srcArr[1] === 2 &&
     srcArr[2] === 3 &&
     srcArr[3] === 4 &&
     srcArr[4] === 5)
  {
    return true;
  }

 }
runTestCase(testcase);
