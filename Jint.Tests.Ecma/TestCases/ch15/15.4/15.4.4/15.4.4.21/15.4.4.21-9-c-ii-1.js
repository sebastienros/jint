/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-1.js
 * @description Array.prototype.reduce - callbackfn called with correct parameters (initialvalue not passed)
 */


function testcase() { 
 
  function callbackfn(prevVal, curVal, idx, obj)
  {
    if(idx > 0 && obj[idx] === curVal && obj[idx-1] === prevVal)
      return curVal;
    else 
      return false;
  }

  var arr = [0,1,true,null,new Object(),"five"];
  if( arr.reduce(callbackfn) === "five") 
    return true;
 }
runTestCase(testcase);
