/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.3/15.12.3-5-a-i-1.js
 * @description JSON.stringify converts Number wrapper object space aruguments to Number values
 */


function testcase() {
  var obj = {a1: {b1: [1,2,3,4], b2: {c1: 1, c2: 2}},a2: 'a2'};
  return JSON.stringify(obj,null, new Number(5))=== JSON.stringify(obj,null, 5);
  }
runTestCase(testcase);
