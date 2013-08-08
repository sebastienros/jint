/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-2.js
 * @description Array.prototype.indexOf must return correct index (Number)
 */


function testcase() {
  var obj = {toString:function (){return 0}};
  var one = 1;
  var _float = -(4/3);
  var a = new Array(false,undefined,null,"0",obj,-1.3333333333333, "str",-0,true,+0, one, 1,0, false, _float, -(4/3));
  if (a.indexOf(-(4/3)) === 14 &&      // a[14]=_float===-(4/3)
      a.indexOf(0) === 7      &&       // a[7] = +0, 0===+0
      a.indexOf(-0) === 7      &&     // a[7] = +0, -0===+0
      a.indexOf(1) === 10 )            // a[10] =one=== 1
  {
    return true;
  }
 }
runTestCase(testcase);
