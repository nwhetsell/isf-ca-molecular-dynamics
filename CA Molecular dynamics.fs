/*{
    "CATEGORIES": [
        "Filter",
        "Generator"
    ],
    "CREDIT": "Mykhailo Moroz <https://www.shadertoy.com/user/michael0884>",
    "DESCRIPTION": "Random slime mold generator, converted from <https://www.shadertoy.com/view/3s3cWr>",
    "INPUTS": [
        {
            "NAME" : "inputImage",
            "TYPE" : "image"
        },
        {
            "NAME": "inputImageAmount",
            "LABEL": "Input image amount",
            "TYPE": "float",
            "DEFAULT": 0,
            "MIN": 0,
            "MAX": 1
        },
        {
            "NAME": "restart",
            "LABEL": "Restart",
            "TYPE": "event"
        },
        {
            "NAME": "dt",
            "LABEL": "Simulation speed",
            "TYPE": "float",
            "DEFAULT": 0.5,
            "MAX": 10,
            "MIN": 0
        },
        {
            "NAME": "cooling",
            "LABEL": "Cooling",
            "TYPE": "float",
            "DEFAULT": 1.5,
            "MAX": 10,
            "MIN": -10
        }
    ],
    "ISFVSN": "2",
    "PASSES": [
        {
            "TARGET": "bufferA_positionAndMass",
            "PERSISTENT": true,
            "FLOAT": true
        },
        {
            "TARGET": "bufferA_velocity",
            "PERSISTENT": true,
            "FLOAT": true
        },
        {
            "TARGET": "bufferB_positionAndMass",
            "PERSISTENT": true,
            "FLOAT": true
        },
        {
            "TARGET": "bufferB_velocity",
            "PERSISTENT": true,
            "FLOAT": true
        },
        {

        }
    ]
}*/

// Constants and functions from LYGIA <https://github.com/patriciogonzalezvivo/lygia>
#define PI 3.1415926535897932384626433832795


//
// ShaderToy Common
//

//useful functions
#define GS(x) exp(-dot(x,x))
#define GS0(x) exp(-length(x))
#define CI(x) smoothstep(1.0, 0.9, length(x))
#define Dir(ang) vec2(cos(ang), sin(ang))
#define Rot(ang) mat2(cos(ang), sin(ang), -sin(ang), cos(ang))
#define loop(i,x) for(int i = 0; i < x; i++)
#define range(i,a,b) for(int i = a; i <= b; i++)


//MD force
float MF(vec2 dx)
{
    return -GS(0.75*dx) + 0.13*GS(0.4*dx);
}


//the step functions need to be exactly like this!! step(x,0) does not work!
float Ha(vec2 x)
{
    return ((x.x >= 0.)?1.:0.)*((x.y >= 0.)?1.:0.);
}

float Hb(vec2 x)
{
    return ((x.x > 0.)?1.:0.)*((x.y > 0.)?1.:0.);
}

//particle distribution
vec3 PD(vec2 x, vec2 pos)
{
    return vec3(x, 1.0)*Ha(x - (pos - 0.5))*Hb((pos + 0.5) - x);
}


//data packing
#define POST_UNPACK(X) (clamp(X, 0., 1.) * 2. - 1.)
#define PRE_PACK(X) clamp(0.5 * X + 0.5, 0., 1.)


float sdBox( in vec2 p, in vec2 b )
{
    vec2 d = abs(p)-b;
    return length(max(d,0.0)) + min(max(d.x,d.y),0.0);
}

float border(vec2 p)
{
    float bound = -sdBox(p - RENDERSIZE*0.5, RENDERSIZE*vec2(0.49, 0.49));
    float box = sdBox((p - RENDERSIZE*vec2(0.5, 0.6)) , RENDERSIZE*vec2(0.05, 0.01));
    float drain = -sdBox(p - RENDERSIZE*vec2(0.5, 0.7), RENDERSIZE*vec2(0.0, 0.0));
    return bound;
}

#define h 1.
vec3 bN(vec2 p)
{
    vec3 dx = vec3(-h,0,h);
    vec4 idx = vec4(-1./h, 0., 1./h, 0.25);
    vec3 r = idx.zyw*border(p + dx.zy)
           + idx.xyw*border(p + dx.xy)
           + idx.yzw*border(p + dx.yz)
           + idx.yxw*border(p + dx.yx);
    return vec3(normalize(r.xy), r.z + 1e-4);
}


vec3 hsv2rgb( in vec3 c )
{
    vec3 rgb = clamp( abs(mod(c.x*6.0+vec3(0.0,4.0,2.0),6.0)-3.0)-1.0, 0.0, 1.0 );

	rgb = rgb*rgb*(3.0-2.0*rgb); // cubic smoothing

	return c.z * mix( vec3(1.0), rgb, c.y);
}


void main()
{
    vec2 position = gl_FragCoord.xy;

    if (PASSINDEX == 0 || PASSINDEX == 1) // ShaderToy Buffer A
    {
        vec2 X = vec2(0);
        vec2 V = vec2(0);
        float M = 0.;

        //basically integral over all updated neighbor distributions
        //that fall inside of this pixel
        //this makes the tracking conservative
        range(i, -1, 1) range(j, -1, 1)
        {
            vec2 tpos = position + vec2(i,j);
            vec4 data = texelFetch(bufferB_positionAndMass, ivec2(mod(tpos, RENDERSIZE)), 0);

            vec2 X0 = POST_UNPACK(data.xy) + tpos;
            vec2 V0 = POST_UNPACK(texelFetch(bufferB_velocity, ivec2(mod(tpos, RENDERSIZE)), 0).xy);
           	int M0 = int(data.z);
            int M0H = M0/2;

            X0 += V0*dt; //integrate position

            //the deposited mass into this cell
            vec3 m = (M0 >= 2)?
                (float(M0H)*PD(X0+vec2(0.5, 0.0), position) + float(M0 - M0H)*PD(X0-vec2(0.5, 0.0), position))
                :(float(M0)*PD(X0, position));

            //add weighted by mass
            X += m.xy;
            V += V0*m.z;

            //add mass
            M += m.z;
        }

        //normalization
        if (M != 0.) {
            X /= M;
            V /= M;
        }

        //initial condition
        if (FRAMEINDEX < 1 || restart) {
            X = position;
            V = vec2(0.);
            M = Ha(position - (RENDERSIZE*0.5 - RENDERSIZE.x*0.15))*Hb((RENDERSIZE*0.5 + RENDERSIZE.x*0.15) - position);
        }

        if (PASSINDEX == 0) {
            X = X - position;
            gl_FragColor = vec4(PRE_PACK(X), M, 1.);
        } else {
            gl_FragColor = vec4(PRE_PACK(V), 0., 1.);
        }
    }
    else if (PASSINDEX == 2 || PASSINDEX == 3) // ShaderToy Buffer B
    {
        vec2 uv = position/RENDERSIZE;

        vec4 data = texelFetch(bufferA_positionAndMass, ivec2(mod(position, RENDERSIZE)), 0);
        vec2 X = POST_UNPACK(data.xy) + position;
        vec2 V = POST_UNPACK(texelFetch(bufferA_velocity, ivec2(mod(position, RENDERSIZE)), 0).xy);
        float M = data.z;

        if(M != 0.) //not vacuum
        {
            //Compute the force
            vec2 Fa = vec2(0.);
            range(i, -2, 2) range(j, -2, 2)
            {
                vec2 tpos = position + vec2(i,j);
                vec4 data = texelFetch(bufferA_positionAndMass, ivec2(mod(tpos, RENDERSIZE)), 0);

                vec2 X0 = POST_UNPACK(data.xy) + tpos;
                vec2 V0 = POST_UNPACK(texelFetch(bufferA_velocity, ivec2(mod(tpos, RENDERSIZE)), 0).xy);
                float M0 = data.z;
                vec2 dx = X0 - X;

                Fa += M0*MF(dx)*dx;
            }

            vec2 F = vec2(0.);
            // if(iMouse.z > 0.)
            // {
            //     vec2 dx= pos - iMouse.xy;
            //         F -= 0.003*dx*GS(dx/30.);
            // }

           	//gravity
            F += 0.001*vec2(0,-1);

            //integrate velocity
            V += (F + Fa)*dt/M;

            //Wyatt thermostat
            X += cooling*Fa*dt/M;

            vec3 BORD = bN(X);
            V += 0.5*smoothstep(0., 5., -BORD.z)*BORD.xy;

            //velocity limit
            float v = length(V);
            V /= (v > 1.)?1.*v:1.;
        }

        //save
        if (PASSINDEX == 2) {
            X = X - position;
            gl_FragColor = vec4(PRE_PACK(X), M, 1.);
        } else {
            gl_FragColor = vec4(PRE_PACK(V), 0., 1.);
        }
    }
    else // ShaderToy Image
    {
        //zoom in
        // if(isKeyPressed(KEY_SPACE))
        // {
        //    	pos = iMouse.xy + pos*zoom - R*zoom*0.5;
        // }
        float rho = 0.001;
        vec2 vel = vec2(0., 0.);

        //compute the smoothed density and velocity
        range(i, -2, 2) range(j, -2, 2)
        {
            vec2 tpos = floor(position) + vec2(i,j);
            vec4 data = texelFetch(bufferA_positionAndMass, ivec2(mod(tpos, RENDERSIZE)), 0);

            vec2 X0 = POST_UNPACK(data.xy) + tpos;
            vec2 V0 = POST_UNPACK(texelFetch(bufferB_velocity, ivec2(mod(tpos, RENDERSIZE)), 0).xy);
            float M0 = data.z;
            vec2 dx = X0 - position;

#define radius 1.0
            float K = GS(dx/radius)/(radius*radius);
            rho += M0*K;
            vel += M0*K*V0;
        }

        vel /= rho;
        vec3 vc = hsv2rgb(vec3(6.*atan(vel.x, vel.y)/(2.*PI), 1.0, rho*length(vel.xy)));
        gl_FragColor.xyz = cos(0.9*vec3(3,2,1)*rho) + 0.*vc;
    }
}
